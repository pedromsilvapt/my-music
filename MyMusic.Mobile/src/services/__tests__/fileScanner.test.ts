import {listFiles, resolveDirectoryPath} from '../../../modules/repo-files';
import {scanFromDirectory} from '../fileScanner';
import {ensureRepositoryAccess} from '../storageAccess';

jest.mock('../../../modules/repo-files', () => ({listFiles: jest.fn(), resolveDirectoryPath: jest.fn()}));
jest.mock('../storageAccess', () => ({
    ensureRepositoryAccess: jest.fn(),
    MISSING_ALL_FILES_ACCESS_MESSAGE: 'All files access is missing',
}));

const list = listFiles as jest.Mock;
const resolveDirectory = resolveDirectoryPath as jest.Mock;
const ensureAccess = ensureRepositoryAccess as jest.Mock;

const MODIFIED = Date.UTC(2024, 0, 15, 10, 30);
const CREATED = Date.UTC(2023, 5, 1, 8, 0);

function listed(relativePath: string, overrides: Partial<{size: number; modifiedAt: number; createdAt: number | null}> = {}) {
    return {relativePath, size: 100, modifiedAt: MODIFIED, createdAt: CREATED, ...overrides};
}

function options(excludePatterns: string[] = []) {
    return {extensions: ['.mp3', '.flac'], excludePatterns, basePath: '/storage/emulated/0/Music'};
}

describe('scanFromDirectory (file system)', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        jest.spyOn(console, 'error').mockImplementation(() => {});
        list.mockResolvedValue({files: [], errors: []});
        ensureAccess.mockResolvedValue(true);
    });

    test('fails without listing when All files access is not granted', async () => {
        ensureAccess.mockResolvedValue(false);

        // An unreadable folder lists as empty: it must not be taken for a folder with no music
        await expect(scanFromDirectory('/music', options())).rejects.toThrow('All files access is missing');
        expect(list).not.toHaveBeenCalled();
    });

    test('lists the folder once the access is granted', async () => {
        let grant: (granted: boolean) => void = () => {};
        ensureAccess.mockReturnValue(new Promise<boolean>((resolve) => {
            grant = resolve;
        }));
        list.mockResolvedValue({files: [listed('a.mp3')], errors: []});

        // The scan waits while the user is asked for the permission
        const scan = scanFromDirectory('/music', options());
        await new Promise((resolve) => setImmediate(resolve));
        expect(list).not.toHaveBeenCalled();

        grant(true);

        expect((await scan).files).toHaveLength(1);
    });

    test('lists a folder picked as a content:// tree through the path Android resolves it to', async () => {
        const uri = 'content://com.android.externalstorage.documents/tree/home%3AMusic';
        resolveDirectory.mockResolvedValue('/storage/emulated/0/Documents/Music');
        list.mockResolvedValue({files: [listed('Artist/Album/a b.mp3', {size: 42})], errors: []});

        const result = await scanFromDirectory(uri, options());

        // The path is asked to Android, never read from the text of the URI
        expect(resolveDirectory).toHaveBeenCalledWith(uri);
        // One native call for the whole folder
        expect(list).toHaveBeenCalledTimes(1);
        expect(list).toHaveBeenCalledWith('/storage/emulated/0/Documents/Music', ['.mp3', '.flac']);
        expect(result.errors).toEqual([]);
        expect(result.files).toEqual([{
            relativePath: 'Artist/Album/a b.mp3',
            fullPath: 'file:///storage/emulated/0/Documents/Music/Artist/Album/a%20b.mp3',
            modifiedAt: new Date(MODIFIED),
            createdAt: new Date(CREATED),
            size: 42,
        }]);
    });

    test('lists a folder given as a file:// URI or a plain path', async () => {
        await scanFromDirectory('file:///storage/emulated/0/Music/', options());
        await scanFromDirectory('/storage/emulated/0/Music', options());

        expect(list).toHaveBeenNthCalledWith(1, '/storage/emulated/0/Music', expect.anything());
        expect(list).toHaveBeenNthCalledWith(2, '/storage/emulated/0/Music', expect.anything());
    });

    test('applies the exclusion rules to everything the native walk lists', async () => {
        list.mockResolvedValue({
            files: [listed('Artist/a.mp3'), listed('Artist/a.tmp.mp3'), listed('Shows/Podcasts/2024/ep1.mp3'), listed('Inbox/b.mp3')],
            errors: [],
        });

        const result = await scanFromDirectory('/music', options(['*.tmp.mp3', '# comment', 'Podcasts/']));

        // The rules never reach the native side: they have a single implementation
        expect(list).toHaveBeenCalledWith('/music', ['.mp3', '.flac']);
        expect(result.files.map(file => file.relativePath)).toEqual(['Artist/a.mp3', 'Inbox/b.mp3']);
    });

    test('ignores what could not be read inside an excluded folder', async () => {
        list.mockResolvedValue({
            files: [],
            errors: [
                {path: 'Podcasts/Locked', error: 'Failed to list directory'},
                {path: '.trash', error: 'Failed to list directory'},
                {path: 'Artist/Locked', error: 'Failed to list directory'},
                {path: '', error: 'Failed to list directory'},
            ],
        });

        const result = await scanFromDirectory('/music', options(['Podcasts/', '.trash/', 'music']));

        // The folder itself is reported with its URI, whatever the rules say
        expect(result.errors).toEqual([
            {path: 'Artist/Locked', error: 'Failed to list directory'},
            {path: '/music', error: 'Failed to list directory'},
        ]);
    });

    test('reports what the native walk could not read and keeps the files it found', async () => {
        list.mockResolvedValue({files: [listed('a.mp3')], errors: [{path: 'Locked', error: 'Failed to list directory'}]});
        const onError = jest.fn();

        const result = await scanFromDirectory('/music', {...options(), onError});

        expect(result.files).toHaveLength(1);
        expect(result.errors).toEqual([{path: 'Locked', error: 'Failed to list directory'}]);
        expect(onError).toHaveBeenCalledWith('Locked', 'Failed to list directory');
    });

    test('reports a failed listing as a scan error', async () => {
        list.mockRejectedValue(new Error('Directory does not exist'));
        const onError = jest.fn();

        const result = await scanFromDirectory('/missing', {...options(), onError});

        expect(result).toEqual({files: [], errors: [{path: '/missing', error: 'Directory does not exist'}]});
        expect(onError).toHaveBeenCalledWith('/missing', 'Directory does not exist');
    });

    test('fails on a content:// folder that has no filesystem path', async () => {
        resolveDirectory.mockResolvedValue(null);

        // No folder to list: it must not be taken for a folder with no music
        await expect(scanFromDirectory('content://com.example.cloud/tree/root', options())).rejects.toThrow('has no path');
        expect(list).not.toHaveBeenCalled();
        expect(ensureAccess).not.toHaveBeenCalled();
    });

    test('falls back to now for a creation time the device cannot tell', async () => {
        list.mockResolvedValue({files: [listed('a.mp3', {createdAt: null})], errors: []});
        const before = Date.now();

        const result = await scanFromDirectory('/music', options());

        expect(result.files[0].createdAt.getTime()).toBeGreaterThanOrEqual(before);
        expect(result.files[0].modifiedAt).toEqual(new Date(MODIFIED));
    });

    test('reports the number of files found once the folder is listed', async () => {
        list.mockResolvedValue({files: [listed('a.mp3'), listed('b.mp3')], errors: []});
        const onProgress = jest.fn();

        await scanFromDirectory('/music', {...options(), onProgress});

        expect(onProgress).toHaveBeenCalledTimes(1);
        expect(onProgress).toHaveBeenCalledWith(2, '/music');
    });
});
