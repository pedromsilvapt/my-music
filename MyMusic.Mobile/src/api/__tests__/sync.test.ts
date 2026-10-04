import {File} from 'expo-file-system';
import {downloadSong} from '../sync';

jest.mock('expo-file-system', () => {
    class File {
        static downloadFileAsync = jest.fn();
        constructor(public uri: string) {}
    }
    return {File};
});

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

const downloadFileAsync = File.downloadFileAsync as jest.Mock;

describe('downloadSong', () => {
    beforeEach(() => downloadFileAsync.mockReset());

    test('streams the song to the destination path, replacing a leftover file', async () => {
        downloadFileAsync.mockResolvedValue(undefined);

        await downloadSong(42, '/music/My Artist/song #1.mp3.tmp');

        expect(downloadFileAsync).toHaveBeenCalledWith(
            'https://music.test/api/songs/42/download',
            expect.objectContaining({uri: 'file:///music/My%20Artist/song%20%231.mp3.tmp'}),
            {headers: {'X-MyMusic-UserId': '7', 'X-MyMusic-UserName': 'pedro'}, idempotent: true}
        );
    });

    test('a failed download is reported with the song id', async () => {
        downloadFileAsync.mockRejectedValue(new Error('response has status: 404'));

        await expect(downloadSong(42, '/music/song.mp3.tmp')).rejects.toThrow('Failed to download song 42: response has status: 404');
    });
});
