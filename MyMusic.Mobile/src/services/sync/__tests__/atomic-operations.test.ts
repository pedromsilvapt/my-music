import {uploadOneFile, downloadOneFile, removeOneFile} from '../atomic-operations';
import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncResult} from '../types';

function createMockApiClient(overrides: Partial<ISyncApiClient> = {}): ISyncApiClient {
    return {
        startSync: jest.fn(),
        checkSync: jest.fn(),
        uploadFile: jest.fn(),
        recordChunk: jest.fn(),
        completeSync: jest.fn(),
        getPendingActions: jest.fn(),
        acknowledgeAction: jest.fn(),
        resolveConflicts: jest.fn(),
        downloadSong: jest.fn(),
        ...overrides,
    } as unknown as ISyncApiClient;
}

function createMockFileOps(overrides: Partial<IFileOps> = {}): IFileOps {
    return {
        fileExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        writeFile: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        readFileBase64: jest.fn().mockResolvedValue('base64content'),
        getModificationTime: jest.fn().mockReturnValue(new Date('2024-01-01T00:00:00Z')),
        ...overrides,
    } as unknown as IFileOps;
}

function createMockUserPrompt(overrides: Partial<IUserPrompt> = {}): IUserPrompt {
    return {
        promptConflictResolution: jest.fn().mockResolvedValue('upload'),
        confirmDeletion: jest.fn().mockResolvedValue(true),
        ...overrides,
    } as unknown as IUserPrompt;
}

function createContext(overrides: Partial<SyncContext> = {}): SyncContext {
    const result: SyncResult = {
        created: 0, updated: 0, skipped: 0,
        downloaded: 0, removed: 0, failed: 0, conflicts: 0,
    };
    return {
        deviceId: 1,
        repositoryPath: '/music',
        decodedRepoPath: '/music',
        options: {
            force: false,
            dryRun: false,
            autoConfirm: false,
            treatConflictsAsErrors: false,
            scannerType: 'fileSystem',
        },
        result,
        uploadedPaths: new Set(),
        pendingDownloadPaths: new Set(),
        ...overrides,
    };
}

describe('uploadOneFile', () => {
    const file = {
        fullPath: '/music/song.mp3',
        relativePath: 'song.mp3',
        modifiedAt: new Date('2024-01-01T00:00:00Z'),
        createdAt: new Date('2023-01-01T00:00:00Z'),
    };

    test('success case returns Created record', async () => {
        const apiClient = createMockApiClient();
        const ctx = createContext();

        const record = await uploadOneFile(apiClient, ctx, file, 'Created', 'new file');

        expect(record.action).toBe('Created');
        expect(record.source).toBe('Device');
        expect(record.filePath).toBe('song.mp3');
        expect(record.reason).toBe('new file');
        expect(ctx.result.created).toBe(1);
        expect(apiClient.uploadFile).toHaveBeenCalledWith(
            1,
            {uri: '/music/song.mp3', name: 'song.mp3'},
            'song.mp3',
            expect.any(String),
            expect.any(String)
        );
    });

    test('failure case returns Error record', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockRejectedValue(new Error('Network error')),
        });
        const ctx = createContext();

        const record = await uploadOneFile(apiClient, ctx, file, 'Created', 'new file');

        expect(record.action).toBe('Error');
        expect(record.source).toBe('Device');
        expect(record.errorMessage).toBe('Network error');
        expect(ctx.result.failed).toBe(1);
        expect(ctx.result.created).toBe(0);
    });

    test('dry-run records Created without API call', async () => {
        const apiClient = createMockApiClient();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const record = await uploadOneFile(apiClient, ctx, file, 'Created', 'new file');

        expect(record.action).toBe('Created');
        expect(ctx.result.created).toBe(1);
        expect(apiClient.uploadFile).not.toHaveBeenCalled();
    });

    test('Updated action increments updated counter', async () => {
        const apiClient = createMockApiClient();
        const ctx = createContext();

        const record = await uploadOneFile(apiClient, ctx, file, 'Updated', 'modified');

        expect(record.action).toBe('Updated');
        expect(ctx.result.updated).toBe(1);
        expect(ctx.result.created).toBe(0);
    });
});

describe('downloadOneFile', () => {
    test('success case downloads, writes, and acknowledges', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        const record = await downloadOneFile(apiClient, fileOps, ctx, 42, 'song.mp3', '/music');

        expect(record).not.toBeNull();
        expect(record!.action).toBe('Downloaded');
        expect(record!.source).toBe('Server');
        expect(record!.songId).toBe(42);
        expect(ctx.result.downloaded).toBe(1);
        expect(fileOps.ensureDirectory).toHaveBeenCalled();
        expect(fileOps.writeFile).toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, {
            devicePath: 'song.mp3',
            modifiedAt: expect.any(String),
        });
    });

    test('replaces existing file before write', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const ctx = createContext();

        await downloadOneFile(apiClient, fileOps, ctx, 42, 'song.mp3', '/music');

        expect(fileOps.deleteFile).toHaveBeenCalledWith('/music/song.mp3');
        expect(fileOps.writeFile).toHaveBeenCalled();
    });

    test('creates parent directory when missing', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        await downloadOneFile(apiClient, fileOps, ctx, 42, 'subdir/song.mp3', '/music');

        expect(fileOps.ensureDirectory).toHaveBeenCalledWith('/music/subdir/song.mp3');
    });

    test('failure case increments failed and returns Error record', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockRejectedValue(new Error('Server error')),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        const record = await downloadOneFile(apiClient, fileOps, ctx, 42, 'song.mp3', '/music');

        expect(record).not.toBeNull();
        expect(record!.action).toBe('Error');
        expect(record!.errorMessage).toBe('Server error');
        expect(ctx.result.failed).toBe(1);
        expect(ctx.result.downloaded).toBe(0);
    });

    test('dry-run records Downloaded without actual download', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const record = await downloadOneFile(apiClient, fileOps, ctx, 42, 'song.mp3', '/music');

        expect(record!.action).toBe('Downloaded');
        expect(ctx.result.downloaded).toBe(1);
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(fileOps.writeFile).not.toHaveBeenCalled();
    });
});

describe('removeOneFile', () => {
    test('with user confirmation deletes and acknowledges', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(true),
        });
        const ctx = createContext();

        const record = await removeOneFile(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music');

        expect(record).not.toBeNull();
        expect(record!.action).toBe('Removed');
        expect(record!.source).toBe('Server');
        expect(ctx.result.removed).toBe(1);
        expect(fileOps.deleteFile).toHaveBeenCalledWith('/music/song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, {devicePath: 'song.mp3'});
    });

    test('with user cancellation does not delete or acknowledge', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(false),
        });
        const ctx = createContext();

        const record = await removeOneFile(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music');

        expect(record).toBeNull();
        expect(ctx.result.removed).toBe(0);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('with autoConfirm skips prompt, deletes', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: false, autoConfirm: true,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const record = await removeOneFile(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music');

        expect(record).not.toBeNull();
        expect(record!.action).toBe('Removed');
        expect(ctx.result.removed).toBe(1);
        expect(userPrompt.confirmDeletion).not.toHaveBeenCalled();
    });

    test('with missing file still acknowledges, returns null', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(false),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const record = await removeOneFile(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music');

        expect(record).toBeNull();
        expect(ctx.result.removed).toBe(0);
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, {devicePath: 'song.mp3'});
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
    });

    test('dry-run does not delete or acknowledge', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: true,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const record = await removeOneFile(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music');

        expect(record).not.toBeNull();
        expect(record!.action).toBe('Removed');
        expect(ctx.result.removed).toBe(1);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });
});