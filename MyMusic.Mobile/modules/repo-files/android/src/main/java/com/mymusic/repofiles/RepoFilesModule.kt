package com.mymusic.repofiles

import android.content.ActivityNotFoundException
import android.content.ContentResolver
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Environment
import android.os.storage.StorageManager
import android.provider.DocumentsContract
import android.provider.Settings
import expo.modules.kotlin.exception.CodedException
import expo.modules.kotlin.exception.Exceptions
import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.FileOutputStream
import java.nio.file.Files
import java.nio.file.LinkOption
import java.nio.file.StandardCopyOption
import java.nio.file.attribute.BasicFileAttributes

/**
 * File operations on the music repository, which lives in shared storage.
 *
 * expo-file-system cannot do them: for paths outside the app sandbox it checks `File.canRead()` /
 * `File.canWrite()`, which are false for a file that does not exist yet, so it rejects every
 * download, move or copy to a new path. These functions use `java.io.File` directly and rely on the
 * "All files access" permission (`MANAGE_EXTERNAL_STORAGE`).
 *
 * Listing is here as well: expo-file-system takes several native calls per file, each one a
 * ContentResolver query when the repository is a `content://` folder, where a single walk does.
 */
class RepoFilesModule : Module() {
  private val httpClient by lazy { OkHttpClient() }

  override fun definition() = ModuleDefinition {
    Name("RepoFiles")

    AsyncFunction<Boolean>("hasAllFilesAccess") {
      // Before Android 11 the classic storage permissions are enough
      Build.VERSION.SDK_INT < Build.VERSION_CODES.R || Environment.isExternalStorageManager()
    }

    AsyncFunction<Unit>("requestAllFilesAccess") {
      if (Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
        return@AsyncFunction
      }

      val context = appContext.reactContext ?: throw Exceptions.ReactContextLost()
      try {
        context.startActivity(
          Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION, Uri.parse("package:${context.packageName}"))
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        )
      } catch (_: ActivityNotFoundException) {
        // Some devices only have the list of all apps
        context.startActivity(
          Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        )
      }
    }

    // AsyncFunction bodies run off the UI thread, so blocking I/O here is fine
    AsyncFunction("downloadFile") { url: String, headers: Map<String, String>, destinationPath: String ->
      val destination = toFile(destinationPath)
      val request = Request.Builder().url(url).apply {
        headers.forEach { (name, value) -> addHeader(name, value) }
      }.build()

      try {
        httpClient.newCall(request).execute().use { response ->
          if (!response.isSuccessful) {
            throw RepoFilesException("response has status: ${response.code}")
          }

          val body = response.body ?: throw RepoFilesException("response body is null")
          body.byteStream().use { input ->
            FileOutputStream(destination).use { output -> input.copyTo(output) }
          }
        }
      } catch (e: Exception) {
        // Never leave a partial file behind
        destination.delete()
        throw if (e is RepoFilesException) e else RepoFilesException("Unable to download to '$destination': ${e.message}", e)
      }
    }

    AsyncFunction("ensureDirectory") { path: String ->
      val directory = toFile(path)
      if (!directory.isDirectory && !directory.mkdirs() && !directory.isDirectory) {
        throw RepoFilesException("Unable to create the directory '$directory'")
      }
    }

    AsyncFunction("moveFile") { fromPath: String, toPath: String ->
      val from = toFile(fromPath)
      val to = toFile(toPath)
      try {
        Files.move(from.toPath(), to.toPath(), StandardCopyOption.REPLACE_EXISTING)
      } catch (e: Exception) {
        throw RepoFilesException("Unable to move '$from' to '$to': ${e.message}", e)
      }
      Unit
    }

    AsyncFunction("copyFile") { fromPath: String, toPath: String ->
      val from = toFile(fromPath)
      val to = toFile(toPath)
      try {
        from.copyTo(to, overwrite = true)
      } catch (e: Exception) {
        throw RepoFilesException("Unable to copy '$from' to '$to': ${e.message}", e)
      }
      Unit
    }

    AsyncFunction("deleteFile") { path: String ->
      val file = toFile(path)
      if (file.exists() && !file.delete()) {
        throw RepoFilesException("Unable to delete '$file'")
      }
    }

    // The time is in milliseconds since the epoch, as a Double: a JS number
    AsyncFunction("setModifiedTime") { path: String, modifiedAt: Double ->
      val file = toFile(path)
      if (!file.setLastModified(modifiedAt.toLong())) {
        throw RepoFilesException("Unable to set the modified time of '$file'")
      }
    }

    AsyncFunction("listFiles") { rootPath: String, extensions: List<String> ->
      val root = toFile(rootPath)
      if (!root.isDirectory) {
        throw RepoFilesException("Directory does not exist")
      }

      val walk = RepositoryWalk(extensions.map { it.lowercase() }.toSet())
      walk.list(root, "")

      mapOf("files" to walk.files, "errors" to walk.errors)
    }

    AsyncFunction("resolveDirectoryPath") { uri: String ->
      val context = appContext.reactContext ?: throw Exceptions.ReactContextLost()
      resolveTreePath(context, Uri.parse(uri))?.path
    }
  }

  // file:// URIs are percent-encoded; anything else is taken as a plain filesystem path
  private fun toFile(pathOrUri: String): File =
    File(if (pathOrUri.startsWith("file://")) Uri.parse(pathOrUri).path ?: pathOrUri else pathOrUri)
}

private const val EXTERNAL_STORAGE_AUTHORITY = "com.android.externalstorage.documents"
private const val DOWNLOADS_AUTHORITY = "com.android.providers.downloads.documents"

/**
 * The filesystem folder behind a `content://` folder of the system folder picker, or null when it
 * has none (a cloud provider, a volume that is not mounted).
 *
 * Android has no call that gives it directly. The document id is read with [DocumentsContract];
 * the system storage provider builds it as `<root>:<path in the root>`, and the folder of each root
 * is asked to the system ([StorageManager], [Environment]), so nothing is assumed of where a volume
 * is mounted or of which user the app runs as.
 */
private fun resolveTreePath(context: Context, uri: Uri): File? {
  if (uri.scheme != ContentResolver.SCHEME_CONTENT) {
    return null
  }

  val documentId = try {
    if (DocumentsContract.isDocumentUri(context, uri)) DocumentsContract.getDocumentId(uri) else DocumentsContract.getTreeDocumentId(uri)
  } catch (_: IllegalArgumentException) {
    return null
  }

  return when (uri.authority) {
    EXTERNAL_STORAGE_AUTHORITY -> {
      val root = externalStorageRoot(context, documentId.substringBefore(':')) ?: return null
      val path = documentId.substringAfter(':', "")
      if (path.isEmpty()) root else File(root, path)
    }

    DOWNLOADS_AUTHORITY -> when {
      documentId == "downloads" -> Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS)
      documentId.startsWith("raw:") -> File(documentId.removePrefix("raw:"))
      // The other ids are rows of the media store, with no folder to walk
      else -> null
    }

    else -> null
  }
}

/** The folder of a root of the system storage provider: the primary storage, the Documents folder or a volume by its UUID. */
private fun externalStorageRoot(context: Context, rootId: String): File? {
  if (rootId == "home") {
    return Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS)
  }

  val storageManager = context.getSystemService(Context.STORAGE_SERVICE) as StorageManager
  val volume = storageManager.storageVolumes.firstOrNull {
    if (rootId == "primary") it.isPrimary else rootId.equals(it.uuid, ignoreCase = true)
  } ?: return null

  if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
    return volume.directory
  }

  // Before Android 11 a volume does not tell its folder
  return if (volume.isPrimary) Environment.getExternalStorageDirectory() else volume.uuid?.let { File("/storage/$it") }
}

/**
 * Collects the files with one of the extensions under a folder, reading each entry's attributes once.
 * The exclusion rules of the sync are not applied here: they have a single implementation, in
 * JavaScript, which filters what this returns.
 */
private class RepositoryWalk(private val extensions: Set<String>) {
  val files = ArrayList<Map<String, Any?>>()
  val errors = ArrayList<Map<String, String>>()

  fun list(directory: File, relativeDirectory: String) {
    val children = try {
      directory.listFiles()
    } catch (_: SecurityException) {
      null
    }

    if (children == null) {
      addError(relativeDirectory, "Failed to list directory")
      return
    }

    for (child in children) {
      val relativePath = if (relativeDirectory.isEmpty()) child.name else "$relativeDirectory/${child.name}"

      try {
        val entry = Entry.read(child)

        if (entry.isDirectory) {
          list(child, relativePath)
        } else if (entry.isFile && extensions.contains(".${child.name.substringAfterLast('.').lowercase()}")) {
          files.add(
            mapOf(
              "relativePath" to relativePath,
              "size" to entry.size.toDouble(),
              "modifiedAt" to entry.modifiedAt.toDouble(),
              "createdAt" to entry.createdAt?.toDouble()
            )
          )
        }
      } catch (e: Exception) {
        addError(relativePath, e.message ?: "Failed to read file metadata")
      }
    }
  }

  private fun addError(path: String, error: String) {
    errors.add(mapOf("path" to path, "error" to error))
  }

  private class Entry(
    val isDirectory: Boolean,
    val isFile: Boolean,
    val size: Long,
    val modifiedAt: Long,
    val createdAt: Long?
  ) {
    companion object {
      fun read(file: File): Entry {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
          return Entry(file.isDirectory, file.isFile, file.length(), file.lastModified(), null)
        }

        // Everything in a single stat; a symbolic link is neither followed nor listed
        val attributes = Files.readAttributes(file.toPath(), BasicFileAttributes::class.java, LinkOption.NOFOLLOW_LINKS)
        return Entry(
          attributes.isDirectory,
          attributes.isRegularFile,
          attributes.size(),
          attributes.lastModifiedTime().toMillis(),
          attributes.creationTime().toMillis()
        )
      }
    }
  }
}

internal class RepoFilesException(message: String, cause: Throwable? = null) : CodedException(message, cause)
