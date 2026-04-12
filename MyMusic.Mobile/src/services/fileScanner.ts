import { Directory, File } from 'expo-file-system';
import * as MediaLibrary from 'expo-media-library';
import { computeRelativePath, decodeToFsPath, isWithinDirectory } from './pathUtils';

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

export async function scanMusicFiles (options: ScanOptions): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const errors: ScanError[] = [];

    try {
        const { status } = await MediaLibrary.requestPermissionsAsync();
        if (status !== 'granted') {
            console.log('Media library permission not granted');
            return { files, errors };
        }

        const repoFsPath = decodeToFsPath(options.basePath);

        const media = await MediaLibrary.getAssetsAsync({
            mediaType: MediaLibrary.MediaType.audio,
            first: 10000,
        });

        for (const asset of media.assets) {
            const filename = asset.filename;
            const ext = '.' + filename.split('.').pop()?.toLowerCase();

            if (!options.extensions.includes(ext)) {
                continue;
            }

            if (shouldExclude(filename, options.excludePatterns)) {
                continue;
            }

            try {
                const assetInfo = await MediaLibrary.getAssetInfoAsync(asset);
                const sourceUri = assetInfo.localUri || asset.uri;

                if (!sourceUri) {
                    if (options.onError) {
                        options.onError(asset.uri, 'Could not get any valid URI for media asset');
                    }
                    errors.push({ path: asset.uri, error: 'Could not get any valid URI for media asset' });
                    continue;
                }

                const filePath = decodeToFsPath(sourceUri);
                const relativePath = computeRelativePath(filePath, repoFsPath, filename);

                console.log('[scanMusicFiles] sourceUri:', sourceUri);
                console.log('[scanMusicFiles] filePath:', filePath);
                console.log('[scanMusicFiles] repoFsPath:', repoFsPath);
                console.log('[scanMusicFiles] isWithinDirectory:', isWithinDirectory(filePath, repoFsPath));
                console.log('[scanMusicFiles] relativePath:', relativePath);

                if (repoFsPath && !isWithinDirectory(filePath, repoFsPath)) {
                    continue;
                }

                files.push({
                    relativePath,
                    fullPath: sourceUri,
                    modifiedAt: fromEpochTimestamp(asset.modificationTime),
                    createdAt: fromEpochTimestamp(asset.creationTime),
                    size: asset.duration || 0,
                });
            } catch (error) {
                const errorMsg = error instanceof Error ? error.message : 'Failed to process asset';
                if (options.onError) {
                    options.onError(asset.uri, errorMsg);
                }
                errors.push({ path: asset.uri, error: errorMsg });
            }
        }
    } catch (error) {
        console.error('Error scanning music files:', error);
        errors.push({
            path: 'media-library',
            error: error instanceof Error ? error.message : 'Unknown error scanning media library'
        });
    }

    return { files, errors };
}

export async function scanFromDirectory (directoryUri: string, options: ScanOptions): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const errors: ScanError[] = [];
    const { onProgress, onError } = options;

    try {
        const directory = new Directory(directoryUri);
        if (!directory.exists) {
            if (onError) {
                onError(directoryUri, 'Directory does not exist');
            }
            return { files, errors };
        }

        const repoFsPath = decodeToFsPath(directoryUri);

        let lastProgressTime = Date.now();
        let lastYieldTime = Date.now();

        // Async generator that yields files as they're found
        async function* scanDirectoryGenerator (dir: Directory, currentPath: string): AsyncGenerator<FileMetadata | null, void, unknown> {
            let items: (Directory | File)[] = [];

            try {
                items = dir.list();
            } catch (error) {
                const errorMsg = error instanceof Error ? error.message : 'Failed to list directory';
                if (onError) {
                    onError(currentPath, errorMsg);
                }
                errors.push({ path: currentPath, error: errorMsg });
                yield null;
                return;
            }

            for (const item of items) {
                const itemPath = currentPath ? `${currentPath}/${item.name}` : item.name;

                if (item instanceof Directory) {
                    // Recursively scan subdirectory
                    yield* scanDirectoryGenerator(item, itemPath);
                } else if (item instanceof File) {
                    const filename = item.name;
                    const ext = '.' + filename.split('.').pop()?.toLowerCase();

                    if (!options.extensions.includes(ext)) {
                        yield null;
                        continue;
                    }

                    if (shouldExclude(item.name, options.excludePatterns)) {
                        yield null;
                        continue;
                    }

                    try {
                        const fileFsPath = decodeToFsPath(item.uri);
                        const relativePath = computeRelativePath(fileFsPath, repoFsPath, filename);

                        console.log('[fileScanner:scanFromDirectory] item.uri:', item.uri);
                        console.log('[fileScanner:scanFromDirectory] fileFsPath:', fileFsPath);
                        console.log('[fileScanner:scanFromDirectory] directoryUri:', directoryUri);
                        console.log('[fileScanner:scanFromDirectory] repoFsPath:', repoFsPath);
                        console.log('[fileScanner:scanFromDirectory] isWithinDirectory:', isWithinDirectory(fileFsPath, repoFsPath));
                        console.log('[fileScanner:scanFromDirectory] relativePath:', relativePath);

                        const fileMetadata: FileMetadata = {
                            relativePath,
                            fullPath: item.uri,
                            modifiedAt: fromEpochTimestamp(item.modificationTime),
                            createdAt: fromEpochTimestamp(item.creationTime),
                            size: item.size || 0,
                        };

                        yield fileMetadata;
                    } catch (error) {
                        const errorMsg = error instanceof Error ? error.message : 'Failed to read file metadata';
                        if (onError) {
                            onError(item.uri, errorMsg);
                        }
                        errors.push({ path: item.uri, error: errorMsg });
                        yield null;
                    }
                }

                // Yield to UI thread every YIELD_INTERVAL_MS to prevent freezing
                const now = Date.now();
                if (now - lastYieldTime >= YIELD_INTERVAL_MS) {
                    await yieldToUI();
                    lastYieldTime = now;
                }
            }
        }

        // Process files from generator with time-based progress updates
        for await (const file of scanDirectoryGenerator(directory, '')) {
            if (file) {
                files.push(file);
            }

            // Update progress every PROGRESS_INTERVAL_MS
            const now = Date.now();
            if (onProgress && now - lastProgressTime >= PROGRESS_INTERVAL_MS) {
                onProgress(files.length, directoryUri);
                lastProgressTime = now;
            }
        }

        // Final progress update
        if (onProgress) {
            onProgress(files.length, directoryUri);
        }
    } catch (error) {
        const errorMsg = error instanceof Error ? error.message : 'Unknown error scanning directory';
        console.error('Error scanning directory:', errorMsg);
        if (onError) {
            onError(directoryUri, errorMsg);
        }
        errors.push({ path: directoryUri, error: errorMsg });
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

function globToRegex (glob: string): RegExp {
    let regex = '^';
    for (const c of glob) {
        regex += c === '*'
            ? '.*'
            : c === '?'
                ? '.'
                : c === '.'
                    ? '\\.'
                    : c === '/'
                        ? '[\\\\/]'
                        : c === '\\'
                            ? '[\\\\/]'
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
