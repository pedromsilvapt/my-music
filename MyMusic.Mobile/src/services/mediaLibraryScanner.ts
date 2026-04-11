import * as MediaLibrary from 'expo-media-library';
import {File} from 'expo-file-system';

export interface FileMetadata {
    relativePath: string;
    fullPath: string;
    modifiedAt: Date;
    createdAt: Date;
    size: number;
}

export interface ScanError {
    path: string;
    error: string;
}

export interface ScanOptions {
    extensions: string[];
    excludePatterns: string[];
    basePath: string;
    onProgress?: (scannedCount: number, currentDir: string) => void;
    onError?: (path: string, error: string) => void;
}

export interface ScanResult {
    files: FileMetadata[];
    errors: ScanError[];
}

const PROGRESS_INTERVAL_MS = 100;
const YIELD_INTERVAL_MS = 16;
const IDLE_CALLBACK_TIMEOUT_MS = 10;
const PAGE_SIZE = 1000;

function safeDate(d: Date): Date {
    return isNaN(d.getTime()) ? new Date() : d;
}

// Helper to yield control to the UI thread using requestIdleCallback with fallback
function yieldToUI(): Promise<void> {
    return new Promise((resolve) => {
        if (typeof requestIdleCallback === 'function') {
            requestIdleCallback(() => resolve(), {timeout: IDLE_CALLBACK_TIMEOUT_MS});
        } else {
            setTimeout(resolve, 0);
        }
    });
}

/**
 * Scan music files using MediaLibrary API.
 * This is much faster than file system scanning on Android because it uses
 * the indexed MediaStore database instead of walking the directory tree.
 */
export async function scanFromDirectory(
    directoryUri: string,
    options: ScanOptions
): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const errors: ScanError[] = [];
    const {onProgress, onError} = options;

    try {
        // Request permissions when starting scan (user requirement #3)
        const {status} = await MediaLibrary.requestPermissionsAsync();
        if (status !== 'granted') {
            const errorMsg = 'Media library permission not granted';
            if (onError) {
                onError('media-library', errorMsg);
            }
            errors.push({path: 'media-library', error: errorMsg});
            return {files, errors};
        }

        let lastProgressTime = Date.now();
        let lastYieldTime = Date.now();
        let hasMore = true;
        let cursor: string | undefined = undefined;
        let processedCount = 0;

        // Get the normalized repository path for filtering
        // If the directory URI is a SAF content URI, decode it to a filesystem path
        // so that isWithinDirectory and relativePath calculations work correctly
        const repoPath = normalizePath(directoryUri);
        const repoFsPath = isContentUri(directoryUri)
            ? decodeSafUriToFilesystemPath(directoryUri)
            : repoPath;

        if (isContentUri(directoryUri) && !repoFsPath) {
            console.warn(
                'Could not decode SAF URI to filesystem path. ' +
                'Directory filtering will be skipped and all audio files will be included. ' +
                `URI: ${directoryUri}`
            );
        }

        // Paginate through all audio files
        while (hasMore) {
            const result = await MediaLibrary.getAssetsAsync({
                mediaType: 'audio',
                first: PAGE_SIZE,
                after: cursor,
            });

            hasMore = result.hasNextPage;
            cursor = result.endCursor;

            // Process each asset
            for (const asset of result.assets) {
                processedCount++;

                try {
                    // Get full file info including local path
                    const assetInfo = await MediaLibrary.getAssetInfoAsync(asset);

                    // Use localUri as primary, fallback to asset.uri (guaranteed to be file:// on Android)
                    const sourceUri = assetInfo.localUri || asset.uri;

                    if (!sourceUri) {
                        if (onError) {
                            onError(
                                asset.uri,
                                'Could not get any valid URI for media asset'
                            );
                        }
                        errors.push({
                            path: asset.uri,
                            error: 'Could not get any valid URI for media asset',
                        });
                        continue;
                    }

                    // Normalize the URI (preserves content:// URIs, converts file:// to path)
                    const filePath = normalizePath(sourceUri);

                    // Check if file is within the repository directory
                    // When repoPath is a SAF URI, use the decoded filesystem path for matching
                    // If decoding failed (empty repoFsPath), skip the directory filter since
                    // MediaLibrary already returns audio files and we can't verify the directory
                    if (!isContentUri(sourceUri) && repoFsPath && !isWithinDirectory(filePath, repoFsPath)) {
                        continue;
                    }

                    // Get extension from filename
                    const filename = asset.filename;
                    const ext = '.' + filename.split('.').pop()?.toLowerCase();

                    // Filter by extensions
                    if (!options.extensions.includes(ext)) {
                        continue;
                    }

                    // Filter by exclude patterns
                    if (shouldExclude(filename, options.excludePatterns)) {
                        continue;
                    }

                    // Calculate relative path from repository root
                    // Use the decoded filesystem path when available to preserve directory structure
                    // Fall back to filename only if we can't determine the relative path
                    let relativePath: string;
                    if (isContentUri(sourceUri)) {
                        if (repoFsPath && isWithinDirectory(filePath, repoFsPath)) {
                            relativePath = filePath.substring(repoFsPath.length);
                        } else if (repoPath && isWithinDirectory(filePath, repoPath)) {
                            relativePath = filePath.substring(repoPath.length);
                        } else {
                            relativePath = filename;
                        }
                    } else {
                        if (repoFsPath && isWithinDirectory(filePath, repoFsPath)) {
                            relativePath = filePath.substring(repoFsPath.length);
                        } else if (repoPath && isWithinDirectory(filePath, repoPath)) {
                            relativePath = filePath.substring(repoPath.length);
                        } else {
                            relativePath = filename;
                        }
                    }

                    // Strip leading slash from relative path
                    if (relativePath.startsWith('/')) {
                        relativePath = relativePath.slice(1);
                    }

                    // Get file size
                    let size = 0;
                    try {
                        const file = new File(filePath);
                        size = file.size || 0;
                    } catch (e) {
                        // Size is not critical, continue with 0
                    }

                    // Add to results
                    files.push({
                        relativePath,
                        fullPath: filePath,
                        modifiedAt: safeDate(asset.modificationTime
                            ? new Date(asset.modificationTime * 1000)
                            : new Date()),
                        createdAt: safeDate(asset.creationTime
                            ? new Date(asset.creationTime * 1000)
                            : new Date()),
                        size,
                    });

                    // Update progress every PROGRESS_INTERVAL_MS
                    const now = Date.now();
                    if (onProgress && now - lastProgressTime >= PROGRESS_INTERVAL_MS) {
                        onProgress(files.length, repoPath);
                        lastProgressTime = now;
                    }

                    // Yield to UI thread periodically
                    if (now - lastYieldTime >= YIELD_INTERVAL_MS) {
                        await yieldToUI();
                        lastYieldTime = now;
                    }
                } catch (error) {
                    const errorMsg =
                        error instanceof Error ? error.message : 'Failed to process asset';
                    if (onError) {
                        onError(asset.uri, errorMsg);
                    }
                    errors.push({path: asset.uri, error: errorMsg});
                }
            }
        }

        // Final progress update
        if (onProgress) {
            onProgress(files.length, repoPath);
        }
    } catch (error) {
        const errorMsg =
            error instanceof Error ? error.message : 'Unknown error scanning media library';
        console.error('Error scanning media library:', errorMsg);
        if (onError) {
            onError('media-library', errorMsg);
        }
        errors.push({path: 'media-library', error: errorMsg});
    }

    return {files, errors};
}

/**
 * Check if a path is a content URI
 */
function isContentUri(path: string): boolean {
    return path.startsWith('content://');
}

/**
 * Decode a SAF (Storage Access Framework) content URI to a filesystem path.
 * SAF URIs on Android follow patterns like:
 *   content://com.android.providers.media.documents/document/primary%3AMusic
 *   content://com.android.providers.media.documents/document/primary%3AMusic%2FSubFolder
 *
 * The "primary" segment maps to /storage/emulated/0/
 * %3A is URL-encoded colon (:) which separates the volume from the path
 * %2F is URL-encoded forward slash (/)
 *
 * Returns the decoded filesystem path, or empty string if decoding fails.
 */
function decodeSafUriToFilesystemPath(uri: string): string {
    try {
        if (!isContentUri(uri)) {
            return '';
        }

        const parsed = new URL(uri);
        const pathSegments = parsed.pathname.split('/').filter(Boolean);

        // Find the document path segment in the SAF URI
        // Pattern: .../document/<volume>:<path>
        const docIndex = pathSegments.indexOf('document');
        if (docIndex === -1 || docIndex + 1 >= pathSegments.length) {
            return '';
        }

        const docPath = decodeURIComponent(pathSegments[docIndex + 1]);

        // Handle "primary:" prefix which maps to /storage/emulated/0/
        if (docPath.startsWith('primary:')) {
            const relativePath = docPath.substring('primary:'.length);
            return relativePath.length > 0
                ? `/storage/emulated/0/${relativePath}`
                : '/storage/emulated/0';
        }

        // Handle "home:" prefix which maps to /storage/emulated/0/
        if (docPath.startsWith('home:')) {
            const relativePath = docPath.substring('home:'.length);
            return relativePath.length > 0
                ? `/storage/emulated/0/${relativePath}`
                : '/storage/emulated/0';
        }

        // Try to handle other volume patterns like "XXXX-XXXX:" (external storage)
        const colonIndex = docPath.indexOf(':');
        if (colonIndex !== -1) {
            const volume = docPath.substring(0, colonIndex);
            const relativePath = docPath.substring(colonIndex + 1);
            // External SD cards appear as /storage/<volume>/
            if (relativePath.length > 0) {
                return `/storage/${volume}/${relativePath}`;
            }
            return `/storage/${volume}`;
        }

        return '';
    } catch {
        return '';
    }
}

/**
 * Normalize a path by removing file:// prefix and trailing slash.
 * Content URIs are preserved as-is since expo-file-system can handle them.
 */
function normalizePath(path: string): string {
    // Preserve content:// URIs as-is (expo-file-system File class can handle them)
    if (isContentUri(path)) {
        return path;
    }
    // Remove file:// prefix
    if (path.startsWith('file://')) {
        path = path.substring(7);
    }
    // Remove trailing slash
    if (path.endsWith('/')) {
        path = path.slice(0, -1);
    }
    return path;
}

/**
 * Check if a file path is within a directory
 */
function isWithinDirectory(filePath: string, directoryPath: string): boolean {
    if (!directoryPath) {
        return false;
    }
    // Ensure directory path ends with / for proper prefix matching
    const normalizedDir = directoryPath.endsWith('/')
        ? directoryPath
        : directoryPath + '/';
    return filePath.startsWith(normalizedDir) || filePath === directoryPath;
}

/**
 * Check if a filename matches any exclude pattern
 */
function shouldExclude(filename: string, patterns: string[]): boolean {
    for (const pattern of patterns) {
        const regex = globToRegex(pattern);
        if (regex.test(filename)) return true;
        if (regex.test('/' + filename)) return true;
    }
    return false;
}

/**
 * Convert glob pattern to regex
 */
function globToRegex(glob: string): RegExp {
    let regex = '^';
    for (const c of glob) {
        regex +=
            c === '*'
                ? '.*'
                : c === '?'
                  ? '.'
                  : c === '.'
                    ? '\\.'
                    : c === '/'
                      ? '[\\/]'
                      : c === '\\'
                        ? '[\\/]'
                        : c === '['
                          ? '['
                          : c === ']'
                            ? ']'
                            : escapeRegExp(c);
    }
    regex += '$';
    return new RegExp(regex, 'i');
}

function escapeRegExp(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
