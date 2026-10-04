import * as repoFiles from '../../../../modules/repo-files';
import {createDefaultFileOps} from '../defaults';

jest.mock('../../../../modules/repo-files', () => ({
    ensureDirectory: jest.fn(),
    moveFile: jest.fn(),
    copyFile: jest.fn(),
    deleteFile: jest.fn(),
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
});
