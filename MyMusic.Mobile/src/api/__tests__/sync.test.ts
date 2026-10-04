import {downloadFile} from '../../../modules/repo-files';
import {downloadSong} from '../sync';

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
