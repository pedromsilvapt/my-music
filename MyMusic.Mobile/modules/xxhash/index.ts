import { requireNativeModule } from 'expo';

interface XxhashNativeModule {
    hashFile(uri: string): Promise<string>;
}

/**
 * Computes the XXH3-128 hash (seed 0) of a local file and returns its canonical bytes as base64,
 * which is how the server formats its `XxHash128` checksums.
 *
 * The file is read and hashed natively, so its contents never cross into JavaScript.
 * Android only; `uri` is a `file://` URI or a plain filesystem path.
 */
export function hashFile(uri: string): Promise<string> {
    // Resolved on use, so a build without the module only fails when a checksum is actually needed
    return requireNativeModule<XxhashNativeModule>('Xxhash').hashFile(uri);
}
