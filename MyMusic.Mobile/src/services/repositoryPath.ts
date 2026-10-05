import { resolveDirectoryPath } from '../../modules/repo-files';
import { isContentUri, normalizePath } from './pathUtils';

export const UNRESOLVED_REPOSITORY_PATH_MESSAGE =
    'The music folder has no path in the storage of the device. Pick a folder of the internal storage or of an SD card.';

/**
 * The filesystem path of the music repository, which the sync reads and writes directly. The folder
 * picker gives a `content://` folder: Android resolves it (`resolveDirectoryPath` of the repo-files
 * module), its path is never guessed from the text of the URI. Throws when the folder has no path,
 * as one of a cloud provider.
 */
export async function resolveRepositoryPath(repositoryUri: string): Promise<string> {
    if (!isContentUri(repositoryUri)) {
        return normalizePath(repositoryUri);
    }

    const path = await resolveDirectoryPath(repositoryUri);
    if (!path) {
        throw new Error(UNRESOLVED_REPOSITORY_PATH_MESSAGE);
    }

    return path;
}
