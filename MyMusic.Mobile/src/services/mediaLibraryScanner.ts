import * as MediaLibrary from 'expo-media-library';
import { File } from 'expo-file-system';
import { computeRelativePath, decodeSafUriToFilesystemPath, decodeToFsPath, isContentUri, isWithinDirectory } from './pathUtils';

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

function fromEpochTimestamp (value: number | null | undefined): Date {
    if (!value) return new Date();
    const ms = value > 1e11 ? value : value * 1000;
    const d = new Date(ms);
    return isNaN(d.getTime()) ? new Date() : d;
}

// Helper to yield control to the UI thread using requestIdleCallback with fallback
function yieldToUI (): Promise<void> {
    return new Promise((resolve) => {
        if (typeof requestIdleCallback === 'function') {
            requestIdleCallback(() => resolve(), { timeout: IDLE_CALLBACK_TIMEOUT_MS });
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
export async function scanFromDirectory (
    directoryUri: string,
    options: ScanOptions
): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const errors: ScanError[] = [];
    const { onProgress, onError } = options;

    try {
        // Request permissions when starting scan (user requirement #3)
        const { status } = await MediaLibrary.requestPermissionsAsync();
        if (status !== 'granted') {
            const errorMsg = 'Media library permission not granted';
            if (onError) {
                onError('media-library', errorMsg);
            }
            errors.push({ path: 'media-library', error: errorMsg });
            return { files, errors };
        }

        let lastProgressTime = Date.now();
        let lastYieldTime = Date.now();
        let hasMore = true;
        let cursor: string | undefined = undefined;
        let processedCount = 0;

        const repoFsPath = isContentUri(directoryUri)
            ? decodeSafUriToFilesystemPath(directoryUri)
            : decodeToFsPath(directoryUri);

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

                    const filePath = decodeToFsPath(sourceUri);

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

                    const relativePath = computeRelativePath(filePath, repoFsPath, filename);

                    console.log('[mediaLibraryScanner] sourceUri:', sourceUri);
                    console.log('[mediaLibraryScanner] filePath:', filePath);
                    console.log('[mediaLibraryScanner] directoryUri:', directoryUri);
                    console.log('[mediaLibraryScanner] repoFsPath:', repoFsPath);
                    console.log('[mediaLibraryScanner] isWithinDirectory:', isWithinDirectory(filePath, repoFsPath));
                    console.log('[mediaLibraryScanner] relativePath:', relativePath);

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
                        fullPath: sourceUri,
                        modifiedAt: fromEpochTimestamp(asset.modificationTime),
                        createdAt: fromEpochTimestamp(asset.creationTime),
                        size,
                    });

                    // Update progress every PROGRESS_INTERVAL_MS
                    const now = Date.now();
                    if (onProgress && now - lastProgressTime >= PROGRESS_INTERVAL_MS) {
                        onProgress(files.length, repoFsPath);
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
                    errors.push({ path: asset.uri, error: errorMsg });
                }
            }
        }

        // Final progress update
        if (onProgress) {
            onProgress(files.length, repoFsPath);
        }
    } catch (error) {
        const errorMsg =
            error instanceof Error ? error.message : 'Unknown error scanning media library';
        console.error('Error scanning media library:', errorMsg);
        if (onError) {
            onError('media-library', errorMsg);
        }
        errors.push({ path: 'media-library', error: errorMsg });
    }

    return { files, errors };
}

function shouldExclude (filename: string, patterns: string[]): boolean {
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
function globToRegex (glob: string): RegExp {
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

function escapeRegExp (str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
