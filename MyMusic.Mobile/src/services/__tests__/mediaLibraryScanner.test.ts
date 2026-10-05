import * as MediaLibrary from 'expo-media-library';
import {resolveDirectoryPath} from '../../../modules/repo-files';
import {scanFromDirectory} from '../mediaLibraryScanner';

jest.mock('expo-media-library', () => ({
    requestPermissionsAsync: jest.fn(),
    getAssetsAsync: jest.fn(),
    getAssetInfoAsync: jest.fn(),
}));
jest.mock('../../../modules/repo-files', () => ({resolveDirectoryPath: jest.fn()}));
jest.mock('expo-file-system', () => ({
    File: class {
        size = 123;
    },
}));

const requestPermissions = MediaLibrary.requestPermissionsAsync as jest.Mock;
const getAssets = MediaLibrary.getAssetsAsync as jest.Mock;
const resolveDirectory = resolveDirectoryPath as jest.Mock;

const REPOSITORY = 'content://com.android.externalstorage.documents/tree/primary%3AMusic';
const MODIFIED = Date.UTC(2024, 0, 15, 10, 30);

function asset(path: string) {
    return {
        id: path,
        filename: path.split('/').pop()!,
        uri: `file://${path}`,
        modificationTime: MODIFIED,
        creationTime: 0,
    };
}

function page(assets: ReturnType<typeof asset>[], endCursor?: string) {
    return {assets, hasNextPage: endCursor !== undefined, endCursor};
}

function options(excludePatterns: string[] = []) {
    return {extensions: ['.mp3'], excludePatterns, basePath: REPOSITORY};
}

describe('scanFromDirectory (media library)', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        jest.spyOn(console, 'error').mockImplementation(() => {});
        requestPermissions.mockResolvedValue({status: 'granted'});
        resolveDirectory.mockResolvedValue('/storage/emulated/0/Music');
    });

    test('keeps the audio files of the repository folder, page after page', async () => {
        getAssets
            .mockResolvedValueOnce(page([
                asset('/storage/emulated/0/Music/Artist/a.mp3'),
                asset('/storage/emulated/0/Ringtones/ring.mp3'),
            ], 'cursor-1'))
            .mockResolvedValueOnce(page([
                asset('/storage/emulated/0/Music/b.mp3'),
                asset('/storage/emulated/0/Music/cover.ogg'),
            ]));

        const result = await scanFromDirectory(REPOSITORY, options());

        expect(getAssets).toHaveBeenCalledTimes(2);
        expect(getAssets.mock.calls[1][0].after).toBe('cursor-1');
        expect(result.errors).toEqual([]);
        expect(result.files).toEqual([
            {
                relativePath: 'Artist/a.mp3',
                fullPath: 'file:///storage/emulated/0/Music/Artist/a.mp3',
                modifiedAt: new Date(MODIFIED),
                createdAt: expect.any(Date),
                size: 123,
            },
            expect.objectContaining({relativePath: 'b.mp3'}),
        ]);
    });

    test('does not ask for the info of each asset', async () => {
        getAssets.mockResolvedValue(page([asset('/storage/emulated/0/Music/a.mp3')]));

        await scanFromDirectory(REPOSITORY, options());

        // The asset of the page already has the path of the file: one call per file is what made the scan slow
        expect(MediaLibrary.getAssetInfoAsync).not.toHaveBeenCalled();
    });

    test('skips the files the exclusion rules match', async () => {
        getAssets.mockResolvedValue(page([
            asset('/storage/emulated/0/Music/Artist/a.mp3'),
            asset('/storage/emulated/0/Music/Podcasts/ep1.mp3'),
        ]));

        const result = await scanFromDirectory(REPOSITORY, options(['Podcasts/']));

        expect(result.files.map(file => file.relativePath)).toEqual(['Artist/a.mp3']);
    });

    test('reports the files found so far between pages and the total at the end', async () => {
        getAssets
            .mockResolvedValueOnce(page([asset('/storage/emulated/0/Music/a.mp3')], 'cursor-1'))
            .mockResolvedValueOnce(page([asset('/storage/emulated/0/Music/b.mp3')]));
        const onProgress = jest.fn();

        await scanFromDirectory(REPOSITORY, {...options(), onProgress});

        expect(onProgress.mock.calls.map(call => call[0])).toEqual([1, 2]);
    });

    test('reports an asset with no URI and a failed page as scan errors', async () => {
        getAssets
            .mockResolvedValueOnce(page([{...asset('/storage/emulated/0/Music/a.mp3'), uri: ''}], 'cursor-1'))
            .mockRejectedValueOnce(new Error('Query failed'));
        const onError = jest.fn();

        const result = await scanFromDirectory(REPOSITORY, {...options(), onError});

        expect(result.errors).toEqual([
            {path: '/storage/emulated/0/Music/a.mp3', error: 'Could not get any valid URI for media asset'},
            {path: 'media-library', error: 'Query failed'},
        ]);
        expect(onError).toHaveBeenCalledTimes(2);
    });

    test('fails without the media library permission', async () => {
        requestPermissions.mockResolvedValue({status: 'denied'});

        // An unreadable library must not be taken for a folder with no music
        await expect(scanFromDirectory(REPOSITORY, options())).rejects.toThrow('Media library permission not granted');
        expect(getAssets).not.toHaveBeenCalled();
    });

    test('fails on a folder that has no filesystem path', async () => {
        resolveDirectory.mockResolvedValue(null);

        // With no folder to filter by, every audio file of the device would be scanned
        await expect(scanFromDirectory('content://com.example.cloud/tree/root', options())).rejects.toThrow('has no path');
        expect(getAssets).not.toHaveBeenCalled();
    });
});
