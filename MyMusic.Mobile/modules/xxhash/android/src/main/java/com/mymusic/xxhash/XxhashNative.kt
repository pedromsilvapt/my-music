package com.mymusic.xxhash

/**
 * JNI entry points of the `mymusic-xxhash` native library (see `src/main/cpp/xxhash_jni.cpp`).
 */
internal object XxhashNative {
  init {
    System.loadLibrary("mymusic-xxhash")
  }

  /**
   * Returns the 16 canonical (big-endian) bytes of the XXH3-128 hash of the file at [path].
   *
   * @throws java.io.IOException when the file cannot be opened or read
   */
  external fun hashFile(path: String): ByteArray
}
