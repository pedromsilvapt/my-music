import * as MediaLibrary from 'expo-media-library';
import { File } from 'expo-file-system';
import { computeRelativePath, isWithinDirectory, normalizePath, toFileUri } from './pathUtils';
import { resolveRepositoryPath } from './repositoryPath';
import { type FileMetadata, type ScanOptions, type ScanResult } from './scanner/types';
import { createScanErrors, fromEpochTimestamp } from './scanner/utils';
import { createExclusionMatcher } from './sync/exclusions';

const PAGE_SIZE = 1000;

export const MISSING_MEDIA_LIBRARY_PERMISSION_MESSAGE = 'Media library permission not granted';

/**
 * Scans the music files of a folder from the audio files Android has indexed, keeping the ones inside it.
 * It throws when the folder has no path or the media library cannot be read, as nothing can be said of
 * the files then.
 */
export async function scanFromDirectory (
    directoryUri: string,
    options: ScanOptions
): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const { errors, report } = createScanErrors(options.onError);
    const { onProgress } = options;

    const repoFsPath = await resolveRepositoryPath(directoryUri);

    const { status } = await MediaLibrary.requestPermissionsAsync();
    if (status !== 'granted') {
        throw new Error(MISSING_MEDIA_LIBRARY_PERMISSION_MESSAGE);
    }

    try {
        let hasMore = true;
        let cursor: string | undefined = undefined;
        const isExcluded = createExclusionMatcher(options.excludePatterns);

        while (hasMore) {
            const result = await MediaLibrary.getAssetsAsync({
                mediaType: 'audio',
                first: PAGE_SIZE,
                after: cursor,
            });

            hasMore = result.hasNextPage;
            cursor = result.endCursor;

            for (const asset of result.assets) {
                try {
                    const filename = asset.filename;
                    const ext = '.' + filename.split('.').pop()?.toLowerCase();

                    if (!options.extensions.includes(ext)) {
                        continue;
                    }

                    // On Android the asset already has the file:// URI of the file, no need to ask for its info
                    if (!asset.uri) {
                        report(asset.id, 'Could not get any valid URI for media asset');
                        continue;
                    }

                    const filePath = normalizePath(asset.uri);

                    if (!isWithinDirectory(filePath, repoFsPath)) {
                        continue;
                    }

                    const relativePath = computeRelativePath(filePath, repoFsPath, filename);

                    if (isExcluded(relativePath)) {
                        continue;
                    }

                    let size = 0;
                    try {
                        const file = new File(toFileUri(filePath));
                        size = file.size || 0;
                    } catch (e) {
                    }

                    files.push({
                        relativePath,
                        fullPath: asset.uri,
                        modifiedAt: fromEpochTimestamp(asset.modificationTime),
                        createdAt: fromEpochTimestamp(asset.creationTime),
                        size,
                    });
                } catch (error) {
                    report(asset.uri || asset.id, error instanceof Error ? error.message : 'Failed to process asset');
                }
            }

            // Each page is awaited, which is where the UI gets to render the progress
            if (onProgress && hasMore) {
                onProgress(files.length, repoFsPath);
            }
        }
    } catch (error) {
        const errorMsg =
            error instanceof Error ? error.message : 'Unknown error scanning media library';
        console.error('Error scanning media library:', errorMsg);
        report('media-library', errorMsg);
    }

    if (onProgress) {
        onProgress(files.length, repoFsPath);
    }

    return { files, errors };
}
