import {resolveDirectoryPath} from '../../../modules/repo-files';
import {resolveRepositoryPath, UNRESOLVED_REPOSITORY_PATH_MESSAGE} from '../repositoryPath';

jest.mock('../../../modules/repo-files', () => ({resolveDirectoryPath: jest.fn()}));

const resolveDirectory = resolveDirectoryPath as jest.Mock;

describe('resolveRepositoryPath', () => {
    beforeEach(() => jest.clearAllMocks());

    test('asks Android for the path of a content:// folder', async () => {
        const uri = 'content://com.android.externalstorage.documents/tree/home%3AMusic';
        resolveDirectory.mockResolvedValue('/storage/emulated/10/Documents/Music');

        // Nothing of the path can be read from the URI: neither the Documents folder nor the user
        expect(await resolveRepositoryPath(uri)).toBe('/storage/emulated/10/Documents/Music');
        expect(resolveDirectory).toHaveBeenCalledWith(uri);
    });

    test('fails for a content:// folder with no path', async () => {
        resolveDirectory.mockResolvedValue(null);

        await expect(resolveRepositoryPath('content://com.example.cloud/tree/root')).rejects.toThrow(UNRESOLVED_REPOSITORY_PATH_MESSAGE);
    });

    test('takes a file:// URI or a plain path as it is', async () => {
        expect(await resolveRepositoryPath('file:///storage/emulated/0/Music/')).toBe('/storage/emulated/0/Music');
        expect(await resolveRepositoryPath('/storage/emulated/0/Music')).toBe('/storage/emulated/0/Music');
        expect(resolveDirectory).not.toHaveBeenCalled();
    });
});
