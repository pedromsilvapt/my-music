import {scanRepositoryPaths} from '../excludedFilesService';
import {getScanner} from '../scannerRegistry';

jest.mock('../scannerRegistry', () => ({getScanner: jest.fn()}));
jest.mock('../configService', () => ({getMusicExtensions: () => ['.mp3']}));

const scanner = jest.fn();

function file(relativePath: string) {
    return {relativePath, fullPath: `file:///music/${relativePath}`, modifiedAt: new Date(), createdAt: new Date(), size: 1};
}

describe('scanRepositoryPaths', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        (getScanner as jest.Mock).mockReturnValue(scanner);
    });

    test('scans the music files of the repository with no exclusion rule', async () => {
        scanner.mockResolvedValue({files: [file('Artist/a.mp3'), file('.trash/b.mp3')], errors: []});

        const paths = await scanRepositoryPaths('file:///music', 'mediaLibrary');

        // Excluded files have to be scanned as well: they are what the rules are tried against
        expect(getScanner).toHaveBeenCalledWith('mediaLibrary');
        expect(scanner).toHaveBeenCalledWith('file:///music', {
            extensions: ['.mp3'],
            excludePatterns: [],
            basePath: 'file:///music',
        });
        expect(paths).toEqual(['Artist/a.mp3', '.trash/b.mp3']);
    });

    test('keeps the files found when only part of the scan failed', async () => {
        scanner.mockResolvedValue({files: [file('a.mp3')], errors: [{path: 'Locked', error: 'Failed to list directory'}]});

        expect(await scanRepositoryPaths('file:///music', 'fileSystem')).toEqual(['a.mp3']);
    });

    test('fails when the scan failed and found nothing', async () => {
        scanner.mockResolvedValue({files: [], errors: [{path: 'file:///music', error: 'Directory does not exist'}]});

        await expect(scanRepositoryPaths('file:///music', 'fileSystem')).rejects.toThrow('Directory does not exist');
    });

    test('returns nothing for an empty repository', async () => {
        scanner.mockResolvedValue({files: [], errors: []});

        expect(await scanRepositoryPaths('file:///music', 'fileSystem')).toEqual([]);
    });
});
