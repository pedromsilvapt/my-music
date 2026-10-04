package com.mymusic.xxhash

import android.net.Uri
import android.util.Base64
import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition

class XxhashModule : Module() {
  override fun definition() = ModuleDefinition {
    Name("Xxhash")

    // AsyncFunction bodies run off the UI thread, so hashing a whole file here is fine
    AsyncFunction("hashFile") { uri: String ->
      // file:// URIs are percent-encoded; anything else is taken as a plain filesystem path
      val path = if (uri.startsWith("file://")) Uri.parse(uri).path ?: uri else uri

      Base64.encodeToString(XxhashNative.hashFile(path), Base64.NO_WRAP)
    }
  }
}
