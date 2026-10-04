// JNI binding that hashes a file with XXH3-128 (seed 0), using the official xxHash implementation.
// The digest is returned in its canonical (big-endian) form, the byte order the server's
// System.IO.Hashing.XxHash128 produces.

#include <jni.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <memory>
#include <new>
#include <string>

#define XXH_INLINE_ALL
#include "xxhash.h"

namespace {

constexpr size_t kBlockSize = 1024 * 1024;

void ThrowIOException(JNIEnv *env, const std::string &message) {
    jclass exceptionClass = env->FindClass("java/io/IOException");
    if (exceptionClass != nullptr) {
        env->ThrowNew(exceptionClass, message.c_str());
    }
}

// Hashes the file at `path` into `digest`. On failure returns false and describes it in `error`.
bool HashFile(const char *path, XXH128_canonical_t *digest, std::string *error) {
    std::unique_ptr<FILE, int (*)(FILE *)> file(fopen(path, "rb"), fclose);
    if (!file) {
        *error = std::string("Cannot open ") + path + ": " + strerror(errno);
        return false;
    }

    std::unique_ptr<XXH3_state_t, XXH_errorcode (*)(XXH3_state_t *)> state(XXH3_createState(), XXH3_freeState);
    std::unique_ptr<unsigned char[]> block(new (std::nothrow) unsigned char[kBlockSize]);
    if (!state || !block || XXH3_128bits_reset(state.get()) == XXH_ERROR) {
        *error = "Cannot allocate the hash state";
        return false;
    }

    size_t read;
    while ((read = fread(block.get(), 1, kBlockSize, file.get())) > 0) {
        if (XXH3_128bits_update(state.get(), block.get(), read) == XXH_ERROR) {
            *error = std::string("Cannot hash ") + path;
            return false;
        }
    }

    if (ferror(file.get())) {
        *error = std::string("Cannot read ") + path + ": " + strerror(errno);
        return false;
    }

    XXH128_canonicalFromHash(digest, XXH3_128bits_digest(state.get()));
    return true;
}

} // namespace

extern "C" JNIEXPORT jbyteArray JNICALL
Java_com_mymusic_xxhash_XxhashNative_hashFile(JNIEnv *env, jobject /* thiz */, jstring jPath) {
    const char *path = env->GetStringUTFChars(jPath, nullptr);
    if (path == nullptr) {
        return nullptr; // OutOfMemoryError already pending
    }

    XXH128_canonical_t digest;
    std::string error;
    const bool hashed = HashFile(path, &digest, &error);
    env->ReleaseStringUTFChars(jPath, path);

    if (!hashed) {
        ThrowIOException(env, error);
        return nullptr;
    }

    jbyteArray result = env->NewByteArray(sizeof(digest.digest));
    if (result != nullptr) {
        env->SetByteArrayRegion(result, 0, sizeof(digest.digest), reinterpret_cast<const jbyte *>(digest.digest));
    }
    return result;
}
