import * as MediaLibrary from 'expo-media-library';
import { File } from 'expo-file-system';
import { computeRelativePath, decodeSafUriToFilesystemPath, decodeToFsPath, isContentUri, isWithinDirectory, toFileUri } from './pathUtils';
import { type FileMetadata, type ScanError, type ScanOptions, type ScanResult } from './scanner/types';
import { fromEpochTimestamp, shouldExclude, yieldToUI } from './scanner/utils';

const PROGRESS_INTERVAL_MS = 100;
const YIELD_INTERVAL_MS = 16;
const PAGE_SIZE = 1000;

export async function scanFromDirectory (
    directoryUri: string,
    options: ScanOptions
): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const errors: ScanError[] = [];
    const { onProgress, onError } = options;

    try {
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

        while (hasMore) {
            const result = await MediaLibrary.getAssetsAsync({
                mediaType: 'audio',
                first: PAGE_SIZE,
                after: cursor,
            });

            hasMore = result.hasNextPage;
            cursor = result.endCursor;

            for (const asset of result.assets) {
                processedCount++;

                try {
                    const assetInfo = await MediaLibrary.getAssetInfoAsync(asset);

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

                    const filename = asset.filename;
                    const ext = '.' + filename.split('.').pop()?.toLowerCase();

                    if (!options.extensions.includes(ext)) {
                        continue;
                    }

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

                    let size = 0;
                    try {
                        const file = new File(toFileUri(filePath));
                        size = file.size || 0;
                    } catch (e) {
                    }

                    files.push({
                        relativePath,
                        fullPath: sourceUri,
                        modifiedAt: fromEpochTimestamp(asset.modificationTime),
                        createdAt: fromEpochTimestamp(asset.creationTime),
                        size,
                    });

                    const now = Date.now();
                    if (onProgress && now - lastProgressTime >= PROGRESS_INTERVAL_MS) {
                        onProgress(files.length, repoFsPath);
                        lastProgressTime = now;
                    }

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
