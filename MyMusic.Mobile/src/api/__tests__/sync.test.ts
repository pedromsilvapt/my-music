import {downloadFile} from '../../../modules/repo-files';
import {apiMultipartRequest} from '../client';
import {downloadSong, uploadFile} from '../sync';

jest.mock('../../../modules/repo-files', () => ({
    downloadFile: jest.fn(),
}));

jest.mock('expo-secure-store', () => ({
    getItemAsync: jest.fn(async (key: string) => ({userId: '7', userName: 'pedro'} as Record<string, string>)[key] ?? null),
}));

jest.mock('../../services/configService', () => ({
    getServerUrl: () => 'https://music.test/api',
}));

jest.mock('../client', () => ({
    apiRequest: jest.fn(),
    apiMultipartRequest: jest.fn(),
}));

const downloadFileMock = downloadFile as jest.Mock;

describe('downloadSong', () => {
    beforeEach(() => downloadFileMock.mockReset());

    test('streams the song natively to the destination path', async () => {
        downloadFileMock.mockResolvedValue(undefined);

        await downloadSong(42, '/music/My Artist/song #1.mp3.tmp');

        expect(downloadFileMock).toHaveBeenCalledWith(
            'https://music.test/api/songs/42/download',
            {'X-MyMusic-UserId': '7', 'X-MyMusic-UserName': 'pedro'},
            '/music/My Artist/song #1.mp3.tmp'
        );
    });

    test('a failed download is reported with the song id', async () => {
        downloadFileMock.mockRejectedValue(new Error('response has status: 404'));

        await expect(downloadSong(42, '/music/song.mp3.tmp')).rejects.toThrow('Failed to download song 42: response has status: 404');
    });
});

describe('uploadFile', () => {
    afterEach(() => jest.restoreAllMocks());

    // React Native on Android rejects a file part without a content type before sending the request
    test.each([
        ['song.mp3', 'audio/mpeg'],
        ['SONG.MP3', 'audio/mpeg'],
        ['song.xyz', 'application/octet-stream'],
    ])('sends %s with the content type %s', async (name, type) => {
        const append = jest.spyOn(FormData.prototype, 'append').mockImplementation(() => undefined);

        await uploadFile(2, 65, {uri: `file:///music/${name}`, name}, `2018/${name}`, '2024-01-01T00:00:00.000Z', '2023-01-01T00:00:00.000Z');

        expect(append).toHaveBeenCalledWith('file', {uri: `file:///music/${name}`, name, type});
        expect(apiMultipartRequest).toHaveBeenCalledWith('/devices/2/sync/65/upload', expect.any(FormData), expect.anything());
    });
});
