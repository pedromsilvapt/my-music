import { listFiles } from '../../modules/repo-files';
import { toFileUri } from './pathUtils';
import { resolveRepositoryPath } from './repositoryPath';
import { type FileMetadata, type ScanOptions, type ScanResult } from './scanner/types';
import { createScanErrors, fromEpochTimestamp } from './scanner/utils';
import { ensureRepositoryAccess, MISSING_ALL_FILES_ACCESS_MESSAGE } from './storageAccess';
import { createExclusionMatcher } from './sync/exclusions';

/**
 * Scans the music files of a folder with a single native walk of it (`listFiles` of the repo-files
 * module). The folder is read as a plain filesystem path, also when it was picked as a `content://`
 * folder: going through the document provider takes a query per file property.
 *
 * Reading the folder that way needs "All files access": the scan asks for it when it is missing.
 * It throws when the folder has no path or the access is not granted, as nothing can be said of
 * the files then.
 */
export async function scanFromDirectory (directoryUri: string, options: ScanOptions): Promise<ScanResult> {
    const files: FileMetadata[] = [];
    const { errors, report } = createScanErrors(options.onError);

    const repoFsPath = await resolveRepositoryPath(directoryUri);

    // Without the permission the walk finds an empty or partial folder and reports no error, which would
    // read as "the files are gone": fail instead
    if (!(await ensureRepositoryAccess())) {
        throw new Error(MISSING_ALL_FILES_ACCESS_MESSAGE);
    }

    try {
        const isExcluded = createExclusionMatcher(options.excludePatterns);

        // The native walk lists everything: the exclusion rules are only applied here, by the one matcher
        const listed = await listFiles(repoFsPath, options.extensions);

        for (const error of listed.errors) {
            // An empty path is the folder itself
            if (error.path === '') {
                report(directoryUri, error.error);
            } else if (!isExcluded(error.path) && !isExcluded(`${error.path}/`)) {
                // What could not be read inside an excluded folder is not part of the sync
                report(error.path, error.error);
            }
        }

        for (const file of listed.files) {
            if (isExcluded(file.relativePath)) {
                continue;
            }

            files.push({
                relativePath: file.relativePath,
                fullPath: toFileUri(`${repoFsPath}/${file.relativePath}`),
                modifiedAt: fromEpochTimestamp(file.modifiedAt),
                createdAt: fromEpochTimestamp(file.createdAt),
                size: file.size || 0,
            });
        }
    } catch (error) {
        const errorMsg = error instanceof Error ? error.message : 'Unknown error scanning directory';
        console.error('Error scanning directory:', errorMsg);
        report(directoryUri, errorMsg);
    }

    if (options.onProgress) {
        options.onProgress(files.length, directoryUri);
    }

    return { files, errors };
}

export { type ScanOptions, type ScanResult } from './scanner/types';
