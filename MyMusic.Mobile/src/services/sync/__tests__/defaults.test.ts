import * as repoFiles from '../../../../modules/repo-files';
import {createDefaultFileOps} from '../defaults';

jest.mock('../../../../modules/repo-files', () => ({
    ensureDirectory: jest.fn(),
    moveFile: jest.fn(),
    copyFile: jest.fn(),
    deleteFile: jest.fn(),
    setModifiedTime: jest.fn(),
    setModifiedTimes: jest.fn(),
    listFiles: jest.fn(),
}));
jest.mock('../../../../modules/xxhash', () => ({hashFile: jest.fn()}));
jest.mock('expo-file-system', () => ({File: class {}, Directory: class {}}));
jest.mock('expo-keep-awake', () => ({}));
jest.mock('react-native', () => ({Alert: {alert: jest.fn()}}));
jest.mock('../../../api/sync', () => ({}));
jest.mock('../../../api/devices', () => ({}));
jest.mock('../../configService', () => ({}));
jest.mock('../../deviceConfigService', () => ({}));
jest.mock('../../scannerRegistry', () => ({}));
jest.mock('../../../stores/syncStore', () => ({}));

const repo = repoFiles as jest.Mocked<typeof repoFiles>;

describe('createDefaultFileOps', () => {
    const fileOps = createDefaultFileOps();

    beforeEach(() => jest.clearAllMocks());

    test('ensureDirectory creates the parent directory of the given file path', async () => {
        await fileOps.ensureDirectory('/storage/emulated/0/Music/2022/My Song #1.mp3');

        expect(repo.ensureDirectory).toHaveBeenCalledWith('/storage/emulated/0/Music/2022');
    });

    test('writes to the repository go through the repo-files module with plain paths', async () => {
        await fileOps.moveFile('/music/a.mp3.tmp', '/music/a.mp3');
        await fileOps.copyFile('/music/a.mp3', '/music/b.mp3.tmp');
        await fileOps.deleteFile('/music/a.mp3');

        expect(repo.moveFile).toHaveBeenCalledWith('/music/a.mp3.tmp', '/music/a.mp3');
        expect(repo.copyFile).toHaveBeenCalledWith('/music/a.mp3', '/music/b.mp3.tmp');
        expect(repo.deleteFile).toHaveBeenCalledWith('/music/a.mp3');
    });

    test('setModificationTime gives the repo-files module the time in milliseconds', async () => {
        await fileOps.setModificationTime('/music/a.mp3', new Date('2020-01-02T03:04:05Z'));

        expect(repo.setModifiedTime).toHaveBeenCalledWith('/music/a.mp3', 1577934245000);
    });

    test('listModificationTimes reads the dates of the folder in one listing', async () => {
        repo.listFiles.mockResolvedValue({
            files: [{relativePath: 'Artist/a.mp3', size: 1, modifiedAt: 1577934245000, createdAt: null}],
            errors: [{path: 'Locked', error: 'Failed to list directory'}],
        });

        const times = await fileOps.listModificationTimes('/music', ['.mp3']);

        expect(repo.listFiles).toHaveBeenCalledWith('/music', ['.mp3']);
        expect(times).toEqual(new Map([['Artist/a.mp3', new Date('2020-01-02T03:04:05Z')]]));
    });

    test('setModificationTimes gives the repo-files module the paths and times in one call', async () => {
        repo.setModifiedTimes.mockResolvedValue([null, 'denied']);

        const errors = await fileOps.setModificationTimes([
            {path: '/music/a.mp3', modifiedAt: new Date('2020-01-02T03:04:05Z')},
            {path: '/music/b.mp3', modifiedAt: new Date('2020-01-02T03:04:06Z')},
        ]);

        expect(repo.setModifiedTimes).toHaveBeenCalledWith(['/music/a.mp3', '/music/b.mp3'], [1577934245000, 1577934246000]);
        expect(errors).toEqual([null, 'denied']);
    });
});
