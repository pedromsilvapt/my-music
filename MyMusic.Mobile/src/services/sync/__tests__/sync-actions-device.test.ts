import {actionCreateRemote, actionUpdateRemote, actionCreateLocal, actionUpdateLocal, actionDeleteLocal, actionUnlink, actionRename, actionConflict, MAX_RESOLVE_ITEMS_PER_REQUEST} from '../sync-actions-device';
import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncResult, ActionResult, SyncRecordItem} from '../types';
import {addDeltaToResult} from '../types';

const ZERO_COUNTS = {
    createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
    createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0,
    linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0,
    updateTimestampCount: 0, errorCount: 0,
};

function createMockApiClient(overrides: Partial<ISyncApiClient> = {}): ISyncApiClient {
    return {
        startSync: jest.fn(),
        prepareDeduplicate: jest.fn(),
        checkSync: jest.fn(),
        uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, records: [], counts: {...ZERO_COUNTS}}),
        completeSync: jest.fn(),
        createPendingActions: jest.fn(),
        acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS}}),
        resolveConflicts: jest.fn(),
        chooseConflicts: jest.fn(),
        downloadSong: jest.fn(),
        ...overrides,
    } as unknown as ISyncApiClient;
}

function createMockFileOps(overrides: Partial<IFileOps> = {}): IFileOps {
    return {
        fileExists: jest.fn().mockReturnValue(false),
        directoryExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        moveFile: jest.fn().mockResolvedValue(undefined),
        copyFile: jest.fn().mockResolvedValue(undefined),
        computeChecksum: jest.fn().mockResolvedValue('checksum'),
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
        updateLocal: 0, deleteLocal: 0, link: 0,
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
            direction: 'Both',
            deduplicate: false,
        },
        result,
        processedFiles: 0,
        uploadedPaths: new Set(),
        conflictedPaths: new Set(),
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
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, records: [], counts: {...ZERO_COUNTS, createRemoteCount: 1}}),
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
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, records: [], counts: {...ZERO_COUNTS, createRemoteCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
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
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, records: [], counts: {...ZERO_COUNTS, updateRemoteCount: 1}}),
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
            uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, records: [], counts: {...ZERO_COUNTS, updateRemoteCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
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
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(undefined),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(result!.source).toBe('Server');
        expect(result!.songId).toBe(42);
        expect(ctx.result.createLocal).toBe(1);
        expect(fileOps.ensureDirectory).toHaveBeenCalled();
        expect(apiClient.downloadSong).toHaveBeenCalledWith(42, '/music/song.mp3.tmp');
        expect(fileOps.deleteFile).not.toHaveBeenCalledWith('/music/song.mp3');
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/song.mp3.tmp', '/music/song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {
            recordIds: [1],
            modifiedAt: expect.any(String),
        });
    });

    test.each([false, true])('reports an error when the file already exists (dryRun=%s)', async (dryRun) => {
        // An unexpected local file is never overwritten by a create
        const apiClient = createMockApiClient({
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: { force: false, dryRun, autoConfirm: false, treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false },
        });

        const result = await actionCreateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);

        expect(result!.action).toBe('Error');
        expect(result!.errorMessage).toBe('File already exists');
        expect(apiClient.reportSyncError).toHaveBeenCalledWith(1, 1, expect.objectContaining({recordId: 1, filePath: 'song.mp3', songId: 42, errorMessage: 'File already exists'}));
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
    });

    test('failure case returns Error action without incrementing ctx.result', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockRejectedValue(new Error('Server error')),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);

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
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });

        const result = await actionCreateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result!.action).toBe('CreateLocal');
        expect(ctx.result.createLocal).toBe(1);
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1], modifiedAt: undefined});
    });

    test('with reason parameter passes reason to result', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(undefined),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, createLocalCount: 1}}),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, ctx, 42, 'new-song.mp3', '/music', 1, "Server-initiated download; renamed from 'old-song.mp3'");

        expect(result).not.toBeNull();
        expect(result!.action).toBe('CreateLocal');
        expect(result!.reason).toBe("Server-initiated download; renamed from 'old-song.mp3'");
        expect(apiClient.downloadSong).toHaveBeenCalledWith(expect.any(Number), '/music/new-song.mp3.tmp');
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/new-song.mp3.tmp', '/music/new-song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {
            recordIds: [1],
            modifiedAt: expect.any(String),
        });
    });

    test('returns recordId from input parameter', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(undefined),
        });
        const fileOps = createMockFileOps();
        const ctx = createContext();

        const result = await actionCreateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 42);

        expect(result).not.toBeNull();
        expect(result!.recordId).toBe(42);
    });
});

describe('actionUpdateLocal', () => {
    // The temp file exists only between the write and the move
    function fileOpsWithLocalFile(overrides: Partial<IFileOps> = {}) {
        return createMockFileOps({
            fileExists: jest.fn((path: string) => !path.endsWith('.tmp')),
            ...overrides,
        });
    }

    test('replaces the existing file without a prompt and acknowledges', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockResolvedValue(undefined),
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, updateLocalCount: 1}}),
        });
        const fileOps = fileOpsWithLocalFile();
        const ctx = createContext();

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result!.action).toBe('UpdateLocal');
        expect(result!.source).toBe('Server');
        expect(result!.reason).toBe('Server-initiated update');
        expect(ctx.result.updateLocal).toBe(1);
        expect(apiClient.downloadSong).toHaveBeenCalledWith(42, '/music/song.mp3.tmp');
        expect(fileOps.deleteFile).toHaveBeenCalledWith('/music/song.mp3');
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/song.mp3.tmp', '/music/song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1], modifiedAt: expect.any(String)});
    });

    test('reports an error when the file does not exist', async () => {
        const apiClient = createMockApiClient({
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(false) });
        const ctx = createContext();

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);

        expect(result!.action).toBe('Error');
        expect(result!.errorMessage).toBe('File not found');
        expect(apiClient.reportSyncError).toHaveBeenCalledWith(1, 1, expect.objectContaining({recordId: 1, filePath: 'song.mp3', songId: 42, errorMessage: 'File not found'}));
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('dry-run acknowledges without downloading or touching the file', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, updateLocalCount: 1}}),
        });
        const fileOps = fileOpsWithLocalFile();
        const ctx = createContext({
            options: { force: false, dryRun: true, autoConfirm: false, treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false },
        });

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);

        expect(result!.action).toBe('UpdateLocal');
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(fileOps.moveFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1], modifiedAt: undefined});
    });

    test('a failed download reports an error and keeps the existing file', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockRejectedValue(new Error('network down')),
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = fileOpsWithLocalFile();
        const ctx = createContext();

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, 42, 'song.mp3', '/music', 1);

        expect(result!.action).toBe('Error');
        expect(result!.reason).toBe('Server-initiated update failed');
        expect(fileOps.deleteFile).not.toHaveBeenCalledWith('/music/song.mp3');
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('with a local source, copies that file instead of downloading', async () => {
        // A soundalike of a file uploaded in this session is replaced by that file
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, updateLocalCount: 1}}),
        });
        const fileOps = fileOpsWithLocalFile();
        const ctx = createContext();

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, null, 'copy.mp3', '/music', 1, undefined, 'first.mp3');

        expect(result!.action).toBe('UpdateLocal');
        expect(fileOps.copyFile).toHaveBeenCalledWith('/music/first.mp3', '/music/copy.mp3.tmp');
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/copy.mp3.tmp', '/music/copy.mp3');
        expect(apiClient.downloadSong).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1], modifiedAt: expect.any(String)});
    });

    test('with a missing local source, reports an error and keeps the file', async () => {
        const apiClient = createMockApiClient({
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn((path: string) => path === '/music/copy.mp3') });
        const ctx = createContext();

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, null, 'copy.mp3', '/music', 1, undefined, 'first.mp3');

        expect(result!.action).toBe('Error');
        expect(fileOps.copyFile).not.toHaveBeenCalled();
        expect(fileOps.moveFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('with a local source in dry-run, acknowledges without copying', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, updateLocalCount: 1}}),
        });
        const fileOps = fileOpsWithLocalFile();
        const ctx = createContext({
            options: { force: false, dryRun: true, autoConfirm: false, treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false },
        });

        const result = await actionUpdateLocal(apiClient, fileOps, ctx, null, 'copy.mp3', '/music', 1, undefined, 'first.mp3');

        expect(result!.action).toBe('UpdateLocal');
        expect(fileOps.copyFile).not.toHaveBeenCalled();
        expect(fileOps.moveFile).not.toHaveBeenCalled();
    });
});

describe('actionDeleteLocal', () => {
    test('with user confirmation deletes and acknowledges', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteLocalCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(true),
        });
        const ctx = createContext();

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('DeleteLocal');
        expect(result!.source).toBe('Server');
        expect(ctx.result.deleteLocal).toBe(1);
        expect(fileOps.deleteFile).toHaveBeenCalledWith('/music/song.mp3');
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1]});
    });

    test('with user declining reports an error for its record and does not delete', async () => {
        // The user keeps the file, so the record is reported as an Error and the next sync asks again
        const apiClient = createMockApiClient({
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt({
            confirmDeletion: jest.fn().mockResolvedValue(false),
        });
        const ctx = createContext({sessionId: 7});

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', 3, 42);

        expect(result?.action).toBe('Error');
        expect(result?.errorMessage).toBe('Deletion declined by user');
        expect(result?.counts?.errorCount).toBe(1);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(apiClient.reportSyncError).toHaveBeenCalledWith(1, 7, expect.objectContaining({recordId: 42, filePath: 'song.mp3', songId: 3}));
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('dry-run does not prompt', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);

        expect(result?.action).toBe('DeleteLocal');
        expect(userPrompt.confirmDeletion).not.toHaveBeenCalled();
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
    });

    test('with autoConfirm skips prompt', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteLocalCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: false, autoConfirm: true,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('DeleteLocal');
        expect(ctx.result.deleteLocal).toBe(1);
        expect(userPrompt.confirmDeletion).not.toHaveBeenCalled();
    });

    test('with missing file still acknowledges, returns null', async () => {
        // The file is already gone: the record is acknowledged, and its counts are not added (as the CLI does)
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteLocalCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(false),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext();

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 99);

        expect(result).toBeNull();
        expect(ctx.result.deleteLocal).toBe(0);
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
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 99);

        expect(result).toBeNull();
        expect(ctx.result.deleteLocal).toBe(0);
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [99]});
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
    });

    test('dry-run does not delete but acknowledges', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteLocalCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: true,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 1);
        if (result && result.counts) ctx.result = addDeltaToResult(ctx.result, result.counts);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('DeleteLocal');
        expect(ctx.result.deleteLocal).toBe(1);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).toHaveBeenCalledWith(1, 1, {recordIds: [1]});
    });

    test('returns recordId from input parameter', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, deleteLocalCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const userPrompt = createMockUserPrompt();
        const ctx = createContext({options: {force: false, dryRun: false, autoConfirm: true, treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false}});

        const result = await actionDeleteLocal(apiClient, fileOps, userPrompt, ctx, 'song.mp3', '/music', undefined, 99);

        expect(result).not.toBeNull();
        expect(result!.recordId).toBe(99);
    });
});

describe('actionUnlink', () => {
    test('acknowledges only, does not delete file', async () => {
        const mockAcknowledge = jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, unlinkCount: 1}});
        const apiClient = createMockApiClient({
            acknowledgeAction: mockAcknowledge,
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
            deleteFile: jest.fn(),
        });
        const ctx = createContext();

        const result = await actionUnlink(apiClient, ctx, 'song.mp3', 1, 5, 'Orphaned');

        expect(result).not.toBeNull();
        expect(result!.action).toBe('Unlink');
        expect(result!.recordId).toBe(5);
        expect(fileOps.deleteFile).not.toHaveBeenCalled();
        expect(mockAcknowledge).toHaveBeenCalledWith(1, 1, { recordIds: [5] });
    });

    test('does not require fileOps or userPrompt', async () => {
        const mockAcknowledge = jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, unlinkCount: 1}});
        const apiClient = createMockApiClient({
            acknowledgeAction: mockAcknowledge,
        });
        const ctx = createContext();

        const result = await actionUnlink(apiClient, ctx, 'gone.mp3', undefined, 10);

        expect(result).not.toBeNull();
        expect(result!.action).toBe('Unlink');
        expect(mockAcknowledge).toHaveBeenCalledWith(1, 1, { recordIds: [10] });
    });
});

describe('actionRename', () => {
    test('moves file and acknowledges', async () => {
        const apiClient = createMockApiClient({
            acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {...ZERO_COUNTS, renameCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn((path: string) => path === '/music/old-song.mp3'),
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
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
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

    test('reports a failure instead of replacing a file at the new path', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const ctx = createContext();

        const result = await actionRename(apiClient, fileOps, ctx, 'new-song.mp3', 'old-song.mp3', '/music', 1);

        expect(result.action).toBe('Error');
        expect(fileOps.moveFile).not.toHaveBeenCalled();
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    });

    test('renames a file whose new path only differs in case', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
        });
        const ctx = createContext();

        const result = await actionRename(apiClient, fileOps, ctx, 'Song.mp3', 'song.mp3', '/music', 1);

        expect(result.action).toBe('Rename');
        expect(fileOps.moveFile).toHaveBeenCalledWith('/music/song.mp3', '/music/Song.mp3');
    });

    test('failure returns Error action without mutating ctx.result', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({
            fileExists: jest.fn((path: string) => path === '/music/old-song.mp3'),
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
    const conflictRecords: SyncRecordItem[] = [
        { id: 7, filePath: 'song.mp3', action: 'Conflict', songId: 42, data: { localModifiedAt: '2024-06-01T00:00:00Z', serverModifiedAt: '2024-06-02T00:00:00Z', serverChecksumAlgorithm: 'XxHash128' }, reason: null, acknowledged: false, processedAt: '' },
    ];

    test('auto-resolved conflicts add to toUpdate set', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                records: [
                    { id: 1, filePath: 'song.mp3', action: 'UpdateRemote', songId: 42, data: null, resolvesConflictRecordId: null, reason: 'Auto-resolved', acknowledged: false, processedAt: '2024-01-01T00:00:00Z' },
                ],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect(toUpdatePaths.has('song.mp3')).toBe(true);
        expect(result.records).toHaveLength(1);
        expect(result.records[0].action).toBe('UpdateRemote');
    });

    test('treatConflictsAsErrors increments failed without prompt', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                records: [
                    { id: 1, filePath: 'song.mp3', action: 'Conflict', songId: 42, data: { localModifiedAt: '2024-06-01', serverModifiedAt: '2024-06-02' }, resolvesConflictRecordId: null, reason: 'Checksum mismatch', acknowledged: false, processedAt: '2024-01-01T00:00:00Z' },
                ],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: {
                force: false, dryRun: false, autoConfirm: false,
                treatConflictsAsErrors: true, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect(ctx.result.conflict).toBe(1);
        expect(ctx.result.error).toBe(1);
        expect(result.records).toHaveLength(1);
        expect(result.records[0].action).toBe('Conflict');
    });

    test('dry-run resolves conflicts via server (parity with non-dry-run)', async () => {
        const resolveConflicts = jest.fn().mockResolvedValue({
            records: [
                { id: 1, filePath: 'song.mp3', action: 'UpdateTimestamp', songId: 42, data: null, resolvesConflictRecordId: null, reason: 'Checksums match', acknowledged: false, processedAt: '2024-01-01T00:00:00Z' },
            ],
            counts: {...ZERO_COUNTS, updateTimestampCount: 1},
        });
        const apiClient = createMockApiClient({ resolveConflicts });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
        });
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect(resolveConflicts).toHaveBeenCalledWith(1, 1, expect.objectContaining({
            conflicts: expect.arrayContaining([expect.objectContaining({ songId: 42 })]),
        }));
        expect(result.records).toHaveLength(1);
        expect(result.records[0].action).toBe('UpdateTimestamp');
        expect(result.counts).toEqual({...ZERO_COUNTS, updateTimestampCount: 1});
        expect(ctx.result.conflict).toBe(0);
        // The counts are returned for the phase to add, not added here
        expect(ctx.result.updateTimestamp).toBe(0);
    });

    test('no conflicts returns empty result', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, [], [], toUpdatePaths, onProgress);

        expect(result.records).toHaveLength(0);
        expect(apiClient.resolveConflicts).not.toHaveBeenCalled();
    });

    const realConflict = { id: 1, filePath: 'song.mp3', action: 'Conflict', songId: 42, data: { localModifiedAt: '2024-06-01', serverModifiedAt: '2024-06-02' }, resolvesConflictRecordId: null, reason: 'Different checksums', acknowledged: false, processedAt: '2024-01-01T00:00:00Z' };

    test('user prompt for upload uploads the local file resolving the conflict', async () => {
        const updateRemote = { id: 2, filePath: 'song.mp3', action: 'UpdateRemote', songId: 42, data: null, resolvesConflictRecordId: 1, reason: 'File re-uploaded (updated)', acknowledged: true, processedAt: '2024-01-01T00:00:00Z' };
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({ records: [realConflict], counts: {...ZERO_COUNTS, conflictCount: 1} }),
            uploadFile: jest.fn().mockResolvedValue({ success: true, songId: 42, records: [updateRemote], counts: {...ZERO_COUNTS, updateRemoteCount: 1, conflictCount: -1} }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('upload'),
        });
        const ctx = createContext();
        const modifiedAt = new Date('2024-06-01T10:00:00Z');
        const createdAt = new Date('2024-01-01T10:00:00Z');
        const files = [{ relativePath: 'song.mp3', fullPath: '/music/song.mp3', modifiedAt, createdAt }];

        const result = await actionConflict(apiClient, fileOps, userPrompt, ctx, conflictRecords, [], new Set<string>(), jest.fn(), files);

        expect(userPrompt.promptConflictResolution).toHaveBeenCalledWith('song.mp3', ['upload', 'download', 'skip']);
        expect(apiClient.uploadFile).toHaveBeenCalledWith(
            1, 1, { uri: '/music/song.mp3', name: 'song.mp3' }, 'song.mp3',
            modifiedAt.toISOString(), createdAt.toISOString(), 1
        );
        expect(apiClient.chooseConflicts).not.toHaveBeenCalled();
        expect(result.records).toEqual([realConflict, updateRemote]);
        expect(result.counts).toMatchObject({ updateRemoteCount: 1, conflictCount: 0 });
        expect(ctx.uploadedPaths.has('song.mp3')).toBe(true);
        expect(ctx.result.error).toBe(0);
    });

    test('a failed upload leaves the conflict unresolved', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({ records: [realConflict], counts: {...ZERO_COUNTS, conflictCount: 1} }),
            uploadFile: jest.fn().mockRejectedValue(new Error('Network error')),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('upload'),
        });
        const ctx = createContext();

        const result = await actionConflict(apiClient, fileOps, userPrompt, ctx, conflictRecords, [], new Set<string>(), jest.fn());

        expect(result.records).toEqual([realConflict]);
        expect(ctx.uploadedPaths.has('song.mp3')).toBe(false);
        expect(ctx.result.error).toBe(1);
    });

    test('user prompt for download asks the server for the records resolving the conflicts', async () => {
        const otherConflict = { ...realConflict, id: 3, filePath: 'other.mp3', songId: 43 };
        const updateLocal = { id: 4, filePath: 'song.mp3', action: 'UpdateLocal', songId: 42, data: null, resolvesConflictRecordId: 1, reason: 'Conflict resolved by user: server version wins', acknowledged: false, processedAt: '2024-01-01T00:00:00Z' };
        const otherUpdateLocal = { ...updateLocal, id: 5, filePath: 'other.mp3', songId: 43, resolvesConflictRecordId: 3 };
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({ records: [realConflict, otherConflict], counts: {...ZERO_COUNTS, conflictCount: 2} }),
            chooseConflicts: jest.fn().mockResolvedValue({ records: [updateLocal, otherUpdateLocal], counts: {...ZERO_COUNTS, updateLocalCount: 2, conflictCount: -2} }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('download'),
        });
        const ctx = createContext();
        const conflicts = [...conflictRecords, { ...conflictRecords[0], id: 99, filePath: 'other.mp3', songId: 43 }];

        const result = await actionConflict(apiClient, fileOps, userPrompt, ctx, conflicts, [], new Set<string>(), jest.fn());

        // One request for all the conflicts of the chunk
        expect(apiClient.chooseConflicts).toHaveBeenCalledTimes(1);
        expect(apiClient.chooseConflicts).toHaveBeenCalledWith(1, 1, { downloadRecordIds: [1, 3] });
        expect(apiClient.uploadFile).not.toHaveBeenCalled();
        expect(result.records).toEqual([realConflict, otherConflict, updateLocal, otherUpdateLocal]);
        expect(result.counts).toMatchObject({ updateLocalCount: 2, conflictCount: 0 });
        expect(ctx.result.error).toBe(0);
    });

    test.each([
        ['Up', ['upload', 'skip']],
        ['Down', ['download', 'skip']],
        ['Both', ['upload', 'download', 'skip']],
    ] as const)('direction %s only offers the choices it can apply', async (direction, choices) => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({ records: [realConflict], counts: {...ZERO_COUNTS, conflictCount: 1} }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('skip'),
        });
        const ctx = createContext();
        ctx.options.direction = direction;

        await actionConflict(apiClient, fileOps, userPrompt, ctx, conflictRecords, [], new Set<string>(), jest.fn());

        expect(userPrompt.promptConflictResolution).toHaveBeenCalledWith('song.mp3', [...choices]);
    });

    test('a dry run prompts like a real sync', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({ records: [realConflict], counts: {...ZERO_COUNTS, conflictCount: 1} }),
            chooseConflicts: jest.fn().mockResolvedValue({ records: [], counts: {...ZERO_COUNTS} }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('download'),
        });
        const ctx = createContext();
        ctx.options.dryRun = true;

        await actionConflict(apiClient, fileOps, userPrompt, ctx, conflictRecords, [], new Set<string>(), jest.fn());

        expect(apiClient.chooseConflicts).toHaveBeenCalledWith(1, 1, { downloadRecordIds: [1] });
    });

    test('user prompt for skip increments failed', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({
                records: [
                    { id: 1, filePath: 'song.mp3', action: 'Conflict', songId: 42, data: { localModifiedAt: '2024-06-01', serverModifiedAt: '2024-06-02' }, resolvesConflictRecordId: null, reason: 'Different checksums', acknowledged: false, processedAt: '2024-01-01T00:00:00Z' },
                ],
                counts: {...ZERO_COUNTS},
            }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const userPrompt = createMockUserPrompt({
            promptConflictResolution: jest.fn().mockResolvedValue('skip'),
        });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        const result = await actionConflict(apiClient, fileOps, userPrompt, ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect(ctx.result.error).toBe(1);
        expect(toUpdatePaths.has('song.mp3')).toBe(false);
    });
});
describe('failed client actions', () => {
    const ctx = () => createContext({sessionId: 7});

    function expectFailureReported(apiClient: ISyncApiClient, recordId: number, filePath: string, songId: number | null | undefined) {
        expect(apiClient.reportSyncError).toHaveBeenCalledTimes(1);
        expect(apiClient.reportSyncError).toHaveBeenCalledWith(1, 7, expect.objectContaining({recordId, filePath, songId}));
        expect(apiClient.acknowledgeAction).not.toHaveBeenCalled();
    }

    it('reports a failed download as an error for its record', async () => {
        const apiClient = createMockApiClient({
            downloadSong: jest.fn().mockRejectedValue(new Error('network down')),
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });

        const result = await actionCreateLocal(apiClient, createMockFileOps(), ctx(), 3, 'song.mp3', '/music', 42);

        expect(result?.action).toBe('Error');
        expectFailureReported(apiClient, 42, 'song.mp3', 3);
    });

    it('reports a failed delete as an error for its record', async () => {
        const apiClient = createMockApiClient({
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
            deleteFile: jest.fn().mockRejectedValue(new Error('locked')),
        });

        const result = await actionDeleteLocal(apiClient, fileOps, createMockUserPrompt(), ctx(), 'song.mp3', '/music', 3, 42);

        expect(result?.action).toBe('Error');
        expectFailureReported(apiClient, 42, 'song.mp3', 3);
    });

    it('reports a failed rename as an error for its record', async () => {
        const apiClient = createMockApiClient({
            reportSyncError: jest.fn().mockResolvedValue({counts: {...ZERO_COUNTS, errorCount: 1}}),
        });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
            moveFile: jest.fn().mockRejectedValue(new Error('locked')),
        });

        const result = await actionRename(apiClient, fileOps, ctx(), 'new.mp3', 'old.mp3', '/music', 42);

        expect(result.action).toBe('Error');
        expectFailureReported(apiClient, 42, 'new.mp3', undefined);
    });
});

describe('actionConflict - progress', () => {
    test('reports each file being hashed and the files settled by the resolve request', async () => {
        const apiClient = createMockApiClient({
            resolveConflicts: jest.fn().mockResolvedValue({ records: [], counts: {...ZERO_COUNTS} }),
        });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const updates = ['a.mp3', 'b.mp3', 'c.mp3'].map((filePath, i) =>
            ({ id: i + 1, filePath, action: 'UpdateLocal', songId: i + 1, data: { serverChecksumAlgorithm: 'XxHash128' }, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem));
        const ctx = createContext({ processedFiles: 10 });
        const onProgress = jest.fn();

        await actionConflict(apiClient, fileOps, createMockUserPrompt(), ctx, [], updates, new Set(), onProgress);

        const reports = onProgress.mock.calls.map(([p]) => p);
        expect(reports.every(p => p.phase === 'resolving')).toBe(true);
        expect(reports.map(p => p.currentFile).filter(Boolean)).toEqual(['Checking conflicts...', 'a.mp3', 'b.mp3', 'c.mp3']);
        expect(reports.map(p => p.processedFiles).filter(Boolean)).toEqual([13]);
        expect(ctx.processedFiles).toBe(13);
    });
});

describe('actionConflict - resolve requests', () => {
    const ALGORITHM = 'XxHash128';

    function conflict(id: number, filePath: string, songId: number | null, data: unknown = { localModifiedAt: '2024-06-01T00:00:00Z', serverModifiedAt: '2024-06-02T00:00:00Z', serverChecksumAlgorithm: ALGORITHM }): SyncRecordItem {
        return { id, filePath, action: 'Conflict', songId, data, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem;
    }

    function potentialUpdate(id: number, filePath: string, songId: number | null, data: unknown = { localModifiedAt: '2024-06-01T00:00:00Z', serverModifiedAt: '2024-06-02T00:00:00Z', lastSyncedAt: '2024-05-01T00:00:00Z', serverChecksumAlgorithm: ALGORITHM }): SyncRecordItem {
        return { id, filePath, action: 'UpdateLocal', songId, data, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem;
    }

    type ResolveRequest = {
        conflicts: Array<{ path: string; songId: number; checksum: string; checksumAlgorithm: string; localModifiedAt: string }>;
        potentialUpdates: Array<{ path: string; songId: number; checksum: string; checksumAlgorithm: string; localModifiedAt: string; lastSyncedAt: string }>;
    };

    // Answers every item of a request with an UpdateTimestamp record
    function answerEachItem(request: ResolveRequest) {
        const items = [...request.conflicts, ...request.potentialUpdates];
        return {
            records: items.map((item, i) => ({ id: 100 + i, filePath: item.path, action: 'UpdateTimestamp', songId: item.songId, data: null, reason: null, acknowledged: false, processedAt: '' })),
            counts: {...ZERO_COUNTS, updateTimestampCount: items.length},
        };
    }

    function requestItemCount(request: ResolveRequest): number {
        return request.conflicts.length + request.potentialUpdates.length;
    }

    // Records every request sent and answers each of its items
    function recordingApiClient(requests: ResolveRequest[]): ISyncApiClient {
        return createMockApiClient({
            resolveConflicts: jest.fn().mockImplementation(async (_d: number, _s: number, request: ResolveRequest) => {
                requests.push(request);
                return answerEachItem(request);
            }),
        });
    }

    test('sends the checksum of each file and its algorithm, never the file content', async () => {
        const requests: ResolveRequest[] = [];
        const computeChecksum = jest.fn().mockImplementation(async (path: string) => `checksum-of-${path}`);
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true), computeChecksum });

        await actionConflict(recordingApiClient(requests), fileOps, createMockUserPrompt(), createContext(),
            [conflict(1, 'c.mp3', 1)], [potentialUpdate(2, 'p.mp3', 2)], new Set(), jest.fn());

        expect(computeChecksum.mock.calls).toEqual([['/music/c.mp3', ALGORITHM], ['/music/p.mp3', ALGORITHM]]);
        expect(requests).toHaveLength(1);
        expect(requests[0].conflicts[0]).toMatchObject({ path: 'c.mp3', checksum: 'checksum-of-/music/c.mp3', checksumAlgorithm: ALGORITHM });
        expect(requests[0].potentialUpdates[0]).toMatchObject({ path: 'p.mp3', checksum: 'checksum-of-/music/p.mp3', checksumAlgorithm: ALGORITHM });
        expect(requests[0].conflicts[0]).not.toHaveProperty('fileContentBase64');
    });

    test('splits requests at the item limit and aggregates records and counts', async () => {
        const requests: ResolveRequest[] = [];
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const conflictCount = MAX_RESOLVE_ITEMS_PER_REQUEST + 1;
        const conflicts = Array.from({ length: conflictCount }, (_, i) => conflict(i + 1, `conflict${i}.mp3`, i + 1));

        const result = await actionConflict(recordingApiClient(requests), fileOps, createMockUserPrompt(), createContext(), conflicts, [], new Set(), jest.fn());

        expect(requests.map(requestItemCount)).toEqual([MAX_RESOLVE_ITEMS_PER_REQUEST, 1]);
        expect(requests.flatMap(r => r.conflicts.map(c => c.path))).toEqual(conflicts.map(c => c.filePath));
        expect(result.records).toHaveLength(conflictCount);
        expect(result.counts?.updateTimestampCount).toBe(conflictCount);
    });

    test('alternates conflicts and potential updates across requests', async () => {
        const requests: ResolveRequest[] = [];
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const conflicts = Array.from({ length: MAX_RESOLVE_ITEMS_PER_REQUEST + 1 }, (_, i) => conflict(i + 1, `c${i}.mp3`, i + 1));
        const potentialUpdates = [potentialUpdate(1001, 'p0.mp3', 1001), potentialUpdate(1002, 'p1.mp3', 1002)];

        await actionConflict(recordingApiClient(requests), fileOps, createMockUserPrompt(), createContext(), conflicts, potentialUpdates, new Set(), jest.fn());

        // A long conflict list must not push every potential update to the last request
        expect(requests.map(requestItemCount)).toEqual([MAX_RESOLVE_ITEMS_PER_REQUEST, 3]);
        expect(requests[0].potentialUpdates.map(p => p.path)).toEqual(['p0.mp3', 'p1.mp3']);
        expect(requests[0].conflicts.slice(0, 3).map(c => c.path)).toEqual(['c0.mp3', 'c1.mp3', 'c2.mp3']);
        expect(requests[1].potentialUpdates).toEqual([]);
    });

    test('a failed later request keeps the records and counts of earlier requests', async () => {
        const resolveConflicts = jest.fn()
            .mockImplementationOnce(async (_d: number, _s: number, request: ResolveRequest) => answerEachItem(request))
            .mockRejectedValueOnce(new Error('Request failed'));
        const apiClient = createMockApiClient({ resolveConflicts });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });
        const conflicts = Array.from({ length: MAX_RESOLVE_ITEMS_PER_REQUEST + 1 }, (_, i) => conflict(i + 1, `c${i}.mp3`, i + 1));

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(), conflicts, [], new Set(), jest.fn());

        expect(resolveConflicts).toHaveBeenCalledTimes(2);
        expect(result.records).toHaveLength(MAX_RESOLVE_ITEMS_PER_REQUEST);
        expect(result.counts?.updateTimestampCount).toBe(MAX_RESOLVE_ITEMS_PER_REQUEST);
    });

    test('conflicts and potential updates with no songId are skipped', async () => {
        const resolveConflicts = jest.fn().mockImplementation(async (_d: number, _s: number, request: ResolveRequest) => answerEachItem(request));
        const apiClient = createMockApiClient({ resolveConflicts });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });

        await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(),
            [conflict(1, 'no-song.mp3', null), conflict(2, 'song.mp3', 42)],
            [potentialUpdate(3, 'no-song-update.mp3', null)],
            new Set(), jest.fn());

        expect(resolveConflicts).toHaveBeenCalledTimes(1);
        const request = resolveConflicts.mock.calls[0][2] as ResolveRequest;
        expect(request.conflicts.map(c => c.path)).toEqual(['song.mp3']);
        expect(request.potentialUpdates).toEqual([]);
    });

    test('only items with no songId sends no request', async () => {
        const apiClient = createMockApiClient();
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });

        const result = await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(),
            [conflict(1, 'no-song.mp3', null)], [], new Set(), jest.fn());

        expect(apiClient.resolveConflicts).not.toHaveBeenCalled();
        expect(result.records).toEqual([]);
    });

    test('items whose record data has no checksum algorithm are skipped without hashing', async () => {
        const resolveConflicts = jest.fn().mockImplementation(async (_d: number, _s: number, request: ResolveRequest) => answerEachItem(request));
        const apiClient = createMockApiClient({ resolveConflicts });
        const computeChecksum = jest.fn().mockResolvedValue('checksum');
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true), computeChecksum });

        await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(),
            [conflict(1, 'no-data.mp3', 1, null), conflict(2, 'song.mp3', 2)],
            [potentialUpdate(3, 'no-algorithm.mp3', 3, { localModifiedAt: '2024-06-01T00:00:00Z' })],
            new Set(), jest.fn());

        expect(computeChecksum).toHaveBeenCalledTimes(1);
        const request = resolveConflicts.mock.calls[0][2] as ResolveRequest;
        expect(request.conflicts.map(c => c.path)).toEqual(['song.mp3']);
        expect(request.potentialUpdates).toEqual([]);
    });

    test('timestamps are read from the record data', async () => {
        const resolveConflicts = jest.fn().mockImplementation(async (_d: number, _s: number, request: ResolveRequest) => answerEachItem(request));
        const apiClient = createMockApiClient({ resolveConflicts });
        const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });

        await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(),
            [conflict(1, 'c.mp3', 1, { localModifiedAt: '2024-06-01T10:00:00Z', serverModifiedAt: '2024-06-02T00:00:00Z', serverChecksumAlgorithm: ALGORITHM })],
            [potentialUpdate(2, 'p.mp3', 2, { localModifiedAt: '2024-07-01T10:00:00Z', serverModifiedAt: '2024-07-02T00:00:00Z', lastSyncedAt: '2024-05-01T10:00:00Z', serverChecksumAlgorithm: ALGORITHM })],
            new Set(), jest.fn());

        const request = resolveConflicts.mock.calls[0][2] as ResolveRequest;
        expect(request.conflicts[0].localModifiedAt).toBe('2024-06-01T10:00:00.000Z');
        expect(request.potentialUpdates[0].localModifiedAt).toBe('2024-07-01T10:00:00.000Z');
        expect(request.potentialUpdates[0].lastSyncedAt).toBe('2024-05-01T10:00:00.000Z');
    });

    test('missing timestamps in the record data fall back to now', async () => {
        const now = new Date('2025-01-01T00:00:00Z');
        jest.useFakeTimers({ now });
        try {
            const resolveConflicts = jest.fn().mockImplementation(async (_d: number, _s: number, request: ResolveRequest) => answerEachItem(request));
            const apiClient = createMockApiClient({ resolveConflicts });
            const fileOps = createMockFileOps({ fileExists: jest.fn().mockReturnValue(true) });

            await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(),
                [conflict(1, 'c.mp3', 1, { serverChecksumAlgorithm: ALGORITHM })],
                [potentialUpdate(2, 'p.mp3', 2, { serverChecksumAlgorithm: ALGORITHM })],
                new Set(), jest.fn());

            const request = resolveConflicts.mock.calls[0][2] as ResolveRequest;
            expect(request.conflicts[0].localModifiedAt).toBe(now.toISOString());
            expect(request.potentialUpdates[0].localModifiedAt).toBe(now.toISOString());
            expect(request.potentialUpdates[0].lastSyncedAt).toBe(now.toISOString());
        } finally {
            jest.useRealTimers();
        }
    });

    test('a file that cannot be hashed is skipped and the rest are still sent', async () => {
        const resolveConflicts = jest.fn().mockImplementation(async (_d: number, _s: number, request: ResolveRequest) => answerEachItem(request));
        const apiClient = createMockApiClient({ resolveConflicts });
        const fileOps = createMockFileOps({
            fileExists: jest.fn().mockReturnValue(true),
            computeChecksum: jest.fn().mockImplementation(async (path: string) => {
                if (path === '/music/unreadable.mp3') throw new Error('Permission denied');
                return 'checksum';
            }),
        });

        await actionConflict(apiClient, fileOps, createMockUserPrompt(), createContext(),
            [conflict(1, 'unreadable.mp3', 1), conflict(2, 'song.mp3', 2)], [], new Set(), jest.fn());

        const request = resolveConflicts.mock.calls[0][2] as ResolveRequest;
        expect(request.conflicts.map(c => c.path)).toEqual(['song.mp3']);
    });
});
