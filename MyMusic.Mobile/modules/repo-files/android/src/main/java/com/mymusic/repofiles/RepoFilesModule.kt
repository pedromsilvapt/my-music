package com.mymusic.repofiles

import android.content.ActivityNotFoundException
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Environment
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
import java.nio.file.StandardCopyOption

/**
 * File operations on the music repository, which lives in shared storage.
 *
 * expo-file-system cannot do them: for paths outside the app sandbox it checks `File.canRead()` /
 * `File.canWrite()`, which are false for a file that does not exist yet, so it rejects every
 * download, move or copy to a new path. These functions use `java.io.File` directly and rely on the
 * "All files access" permission (`MANAGE_EXTERNAL_STORAGE`).
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
  }

  // file:// URIs are percent-encoded; anything else is taken as a plain filesystem path
  private fun toFile(pathOrUri: String): File =
    File(if (pathOrUri.startsWith("file://")) Uri.parse(pathOrUri).path ?: pathOrUri else pathOrUri)
}

internal class RepoFilesException(message: String, cause: Throwable? = null) : CodedException(message, cause)
