import {actionCreateRemote, actionUpdateRemote, actionCreateLocal, actionUpdateLocal, actionDelete, actionRename, actionConflict} from '../sync-actions-device';
import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncResult, ActionResult} from '../types';
import {addDeltaToResult} from '../types';

const ZERO_COUNTS = {
    createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
    createLocalCount: 0, updateLocalCount: 0, deleteCount: 0,
    linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0,
    updateTimestampCount: 0, errorCount: 0,
};

function createMockApiClient(overrides: Partial<ISyncApiClient> = {}): ISyncApiClient {
    return {
        startSync: jest.fn(),
        checkSync: jest.fn(),
        uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, recordId: null, action: null, data: null, counts: {...ZERO_COUNTS}}),
        completeSync: jest.fn(),
        createPendingActions: jest.fn(),
        acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS}}),
        resolveConflicts: jest.fn(),
        downloadSong: jest.fn(),
        ...overrides,
    } as unknown as ISyncApiClient;
}

function createMockFileOps(overrides: Partial<IFileOps> = {}): IFileOps {
    return {
        fileExists: jest.fn().mockReturnValue(false),
        directoryExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        writeFile: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        moveFile: jest.fn().mockResolvedValue(undefined),
        readFileBase64: jest.fn().mockResolvedValue('base64content'),
        getModificationTime: jest.fn().mockReturnValue(new Date('2024-01-01T00:00:00Z')),
        deleteEmptyDirectories: jest.fn().mockResolvedValue(undefined),
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
        createRemote: 0, updateRemote: 0, createLocal: 0,
        updateLocal: 0, delete: 0, link: 0,
        unlink: 0, rename: 0, skipped: 0,
        conflict: 0, updateTimestamp: 0, error: 0,
    };
    return {
        deviceId: 1,
        sessionId: 1,
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
        conflictedSongIds: new Set(),
        ...overrides,
    };
}

const file = {
    fullPath: '/music/song.mp3',
    relativePath: 'song.mp3',
    modifiedAt: new Date('2024-01-01T00:00:00Z'),
    createdAt: new Date('2023-01-01T00:00:00Z'),
};

describe('actionCreateRemote', () => {
    test('success returns CreateRemote action', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, recordId: null, action: null, data: null, counts: {...ZERO_COUNTS, createRemoteCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext();

        const result = await actionCreateRemote(apiClient, fileOps, ctx, file, 'new file');
        if (result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result.action).toBe('CreateRemote');
        expect(result.source).toBe('Device');
        expect(result.filePath).toBe('song.mp3');
        expect(result.reason).toBe('new file');
        expect(ctx.result.createRemote).toBe(1);
        expect(apiClient.uploadFile).toHaveBeenCalledWith(
            1, 1,
            {uri: '/music/song.mp3', name: 'song.mp3'},
            'song.mp3',
            expect.any(String),
            expect.any(String)
        );
    });

    test('failure returns Error action', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockRejectedValue(new Error('Network error')),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext();

        const result = await actionCreateRemote(apiClient, fileOps, ctx, file, 'new file');

        expect(result.action).toBe('Error');
        expect(result.source).toBe('Device');
        expect(result.errorMessage).toBe('Network error');
        expect(ctx.result.error).toBe(0);
        expect(ctx.result.createRemote).toBe(0);
    });

    test('returns Error when file does not exist', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(false) });
        const ctx = createContext();

        const result = await actionCreateRemote(apiClient, fileOps, ctx, file, 'new file');

        expect(result.action).toBe('Error');
        expect(result.errorMessage).toBe('File not found: /music/song.mp3');
        expect(apiClient.uploadFile).not.toHaveBeenCalled();
    });

    test('dry-run records CreateRemote with counts from upload', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, recordId: null, action: null, data: null, counts: {...ZERO_COUNTS, createRemoteCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const result = await actionCreateRemote(apiClient, fileOps, ctx, file, 'new file');
        if (result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result.action).toBe('CreateRemote');
        expect(ctx.result.createRemote).toBe(1);
        expect(apiClient.uploadFile).toHaveBeenCalled();
    });
});

describe('actionUpdateRemote', () => {
    test('success returns UpdateRemote action', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, recordId: null, action: null, data: null, counts: {...ZERO_COUNTS, updateRemoteCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext();

        const result = await actionUpdateRemote(apiClient, fileOps, ctx, file, 'modified');
        if (result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result.action).toBe('UpdateRemote');
        expect(result.source).toBe('Device');
        expect(result.filePath).toBe('song.mp3');
        expect(result.reason).toBe('modified');
        expect(ctx.result.updateRemote).toBe(1);
    });

    test('failure returns Error action', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockRejectedValue(new Error('Upload failed')),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext();

        const result = await actionUpdateRemote(apiClient, fileOps, ctx, file, 'modified');

        expect(result.action).toBe('Error');
        expect(ctx.result.error).toBe(0);
        expect(ctx.result.updateRemote).toBe(0);
    });

    test('returns Error when file does not exist', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(false) });
        const ctx = createContext();

        const result = await actionUpdateRemote(apiClient, fileOps, ctx, file, 'modified');

        expect(result.action).toBe('Error');
        expect(result.errorMessage).toBe('File not found: /music/song.mp3');
        expect(apiClient.uploadFile).not.toHaveBeenCalled();
    });

    test('dry-run records UpdateRemote with counts from upload', async () => {
        const apiClient = createMockApiClient({
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, recordId: null, action: null, data: null, counts: {...ZERO_COUNTS, updateRemoteCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const result = await actionUpdateRemote(apiClient, fileOps, ctx, file, 'modified');
        if (result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result.action).toBe('UpdateRemote');
        expect(ctx.result.updateRemote).toBe(1);
        expect(apiClient.uploadFile).toHaveBeenCalled();
    });
});

describe('actionCreateLocal', () => {
    test('success case downloads, writes, and acknowledges', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(result!.source).toBe('Server');
        expect(result!.songId).toBe(42);
        expect(ctx.result.createLocal).toBe(1);
        expect(fileOps.ensureDirectory).toHaveBeenCalled();
        expect(fileOps.writeFile).toHaveBeenCalledWith('/music/song.mp3.tmp', blob);
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/song.mp3.tmp', '/music/song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {
            recordIds: [1],
            modifiedAt: expect.any(String),
        });
    });

    test('replaces existing file with user confirmation', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(fileOps.deleteFile).toHaveBeenCalledWith('/music/song.mp3');
        expect(fileOps.writeFile).toHaveBeenCalledWith('/music/song.mp3.tmp', blob);
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/song.mp3.tmp', '/music/song.mp3');
        expect(userPrompt.confirmDeletion).toHaveBeenCalledWith('song.mp3');
    });

    test('user cancellation returns null', async () => {
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(false),
        });
        const apiClient = createMockApiClient();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);

        expect(result).toBeNull();
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
    });

    test('autoConfirm skips prompt', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
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

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(userPrompt.confirmDeletion).not.toHaveBeenCalled();
    });

    test('failure case returns Error action without incrementing ctx.result', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockRejectedValue(new Error('Server error')),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('Error');
        expect(result!.errorMessage).toBe('Server error');
        expect(ctx.result.error).toBe(0);
        expect(ctx.result.createLocal).toBe(0);
    });

    test('dry-run returns CreateLocal without actual download', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result!.action).toBe('CreateLocal');
        expect(ctx.result.createLocal).toBe(1);
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(fileOps.writeFile).not.toHaveBeenCalled();
    });

    test('with reason parameter passes reason to result', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'new-song.mp3', '/music', 1, "Server-initiated download; renamed from 'old-song.mp3'");

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(result!.reason).toBe("Server-initiated download; renamed from 'old-song.mp3'");
        expect(fileOps.writeFile).toHaveBeenCalledWith('/music/new-song.mp3.tmp', blob);
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/new-song.mp3.tmp', '/music/new-song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {
            recordIds: [1],
            modifiedAt: expect.any(String),
        });
    });

    test('returns recordId from input parameter', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 42);

        expect(result).not.toBeNull();
        expect(result!.recordId).toBe(42);
    });
});

describe('actionUpdateLocal', () => {
    test('delegates to actionCreateLocal', async () => {
        const blob = new Blob(['audio data']);
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(blob),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionUpdateLocal(apiClient, fileOps, userPrompt, ctx, 42, 'song.mp3', '/music', 1);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(apiClient.downloadSong).toHaveBeenCalled();
    });
});

describe('actionDelete', () => {
    test('with user confirmation deletes and acknowledges', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(true),
        });
        const ctx = createContext();

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('Delete');
        expect(result!.source).toBe('Server');
        expect(ctx.result.delete).toBe(1);
        expect(fileOps.deleteFile).toHaveBeenCalledWith('/music/song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1]});
    });

    test('with user cancellation does not delete', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(false),
        });
        const ctx = createContext();

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);

        expect(result).toBeNull();
        expect(ctx.result.delete).toBe(0);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('with autoConfirm skips prompt', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteCount: 1}}),
        });
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

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('Delete');
        expect(ctx.result.delete).toBe(1);
        expect(userPrompt.confirmDeletion).not.toHaveBeenCalled();
    });

    test('with missing file still acknowledges, returns null', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(false),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 99);

        expect(result).toBeNull();
        expect(ctx.result.delete).toBe(0);
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [99]});
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
    });

    test('with missing file still acknowledges during dry-run', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(false),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 99);

        expect(result).toBeNull();
        expect(ctx.result.delete).toBe(0);
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [99]});
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
    });

    test('dry-run does not delete but acknowledges', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteCount: 1}}),
        });
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

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('Delete');
        expect(ctx.result.delete).toBe(1);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1]});
    });

    test('returns recordId from input parameter', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({options: {force: false, dryRun: false, autoConfirm: true, treatConflictsAsErrors: false, scannerType: 'fileSystem'}});

        const result = await actionDelete(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 99);

        expect(result).not.toBeNull();
        expect(result!.recordId).toBe(99);
    });
});

describe('actionRename', () => {
    test('moves file and acknowledges', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, renameCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const ctx = createContext();

        const result = await actionRename(apiClient, fileOps, ctx, 'new-song.mp3', 'old-song.mp3', '/music', 1);
        if (result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result.action).toBe('Rename');
        expect(result.filePath).toBe('new-song.mp3');
        expect(result.source).toBe('Server');
        expect(result.reason).toBe("Renamed from 'old-song.mp3'");
        expect(fileOps.ensureDirectory).toHaveBeenCalledWith('/music/new-song.mp3');
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/old-song.mp3', '/music/new-song.mp3');
        expect(fileOps.deleteEmptyDirectories).toHaveBeenCalledWith('/music/old-song.mp3', '/music');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {
            recordIds: [1],
        });
    });

    test('dry-run returns Rename with acknowledgment but without file operations', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, renameCount: 1}}),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });

        const result = await actionRename(apiClient, fileOps, ctx, 'new-song.mp3', 'old-song.mp3', '/music', 1);
        if (result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result.action).toBe('Rename');
        expect(fileOps.moveFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1]});
    });

    test('skips move if previous file does not exist', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, renameCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(false),
        });
        const ctx = createContext();

        const result = await actionRename(apiClient, fileOps, ctx, 'new-song.mp3', 'old-song.mp3', '/music', 1);

        expect(result.action).toBe('Rename');
        expect(fileOps.moveFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalled();
    });

    test('failure returns Error action without mutating ctx.result', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
            moveFile: jest.fn().mockRejectedValue(new Error('Move failed')),
        });
        const ctx = createContext();

        const result = await actionRename(apiClient, fileOps, ctx, 'new-song.mp3', 'old-song.mp3', '/music', 1);

        expect(result.action).toBe('Error');
        expect(result.errorMessage).toBe('Move failed');
        expect(ctx.result.error).toBe(0);
    });

    test('returns recordId from input parameter', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const ctx = createContext();

        const result = await actionRename(apiClient, fileOps, ctx, 'new-song.mp3', 'old-song.mp3', '/music', 55);

        expect(result.recordId).toBe(55);
    });
});

describe('actionConflict', () => {
    const chunk = [
        {relativePath: 'song.mp3', fullPath: '/music/song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000},
    ];

    const potentialConflicts = [
        {
            path: 'song.mp3',
            localModifiedAt: new Date('2024-06-01'),
            serverModifiedAt: new Date('2024-06-02'),
            lastSyncedAt: null,
            songId: 42,
            serverChecksum: 'abc123',
            serverChecksumAlgorithm: 'md5',
        },
    ];

    test('auto-resolved conflicts add to toUpdate set', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                resolved: [{path: 'song.mp3', modifiedAt: new Date(), createdAt: new Date(), reason: 'Checksum match'}],
                toUpload: [{path: 'song.mp3', modifiedAt: new Date(), createdAt: new Date(), reason: 'Auto-resolved'}],
                conflicts: [],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(toUpdatePaths.has('song.mp3')).toBe(true);
        expect(result.conflicts).toBe(0);
    });

    test('treatConflictsAsErrors increments failed without prompt', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                resolved: [],
                toUpload: [],
                conflicts: [{path: 'song.mp3', reason: 'Different checksums'}],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext({
            options: {
                force: false, dryRun: false, autoConfirm: false,
                treatConflictsAsErrors: true, scannerType: 'fileSystem',
            },
        });
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.result.conflict).toBe(1);
        expect(ctx.result.error).toBe(1);
        expect(result.conflicts).toBe(1);
    });

    test('dry-run counts conflicts without resolving', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.result.conflict).toBe(1);
        expect(result.conflicts).toBe(1);
        expect(apiClient.resolveConflicts).not.toHaveBeenCalled();
    });

    test('no conflicts returns empty result', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, [], [], toUpdatePaths, onProgress);

        expect(result.conflicts).toBe(0);
        expect(apiClient.resolveConflicts).not.toHaveBeenCalled();
    });

    test('user prompt for upload adds to toUpdate', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                resolved: [],
                toUpload: [],
                conflicts: [{path: 'song.mp3', reason: 'Different checksums'}],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('upload'),
        });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, userPrompt, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(toUpdatePaths.has('song.mp3')).toBe(true);
        expect(userPrompt.promptConflictResolution).toHaveBeenCalledWith('song.mp3');
    });

    test('user prompt for skip increments failed', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                resolved: [],
                toUpload: [],
                conflicts: [{path: 'song.mp3', reason: 'Different checksums'}],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps();
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('skip'),
        });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, userPrompt, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.result.error).toBe(1);
        expect(toUpdatePaths.has('song.mp3')).toBe(false);
    });
});