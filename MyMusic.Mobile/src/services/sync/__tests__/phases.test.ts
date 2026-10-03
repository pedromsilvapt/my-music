import { resolveConflictsPhase, completePhase, uploadPhase, serverActionsPhase, startSessionPhase, prepareDeduplicatePhase } from '../phases';
import { actionCreateRemote, actionUpdateRemote, actionCreateLocal, actionUpdateLocal, actionDeleteLocal, actionUnlink, actionConflict, actionRename, reportFailure } from '../sync-actions-device';
import type { SyncDeps, SyncContext, SyncResult, IFileOps, ISyncApiClient, ISyncConfig, ISyncState, IFileSystemScanner, IKeepAwake, IUserPrompt, SyncRecordItem } from '../types';
import type { RenameData } from '../../../api/types';

jest.mock('../errors', () => ({
    SyncCancelledError: class SyncCancelledError extends Error {
        constructor () {
            super('Sync was cancelled');
            this.name = 'SyncCancelledError';
        }
    },
}));

jest.mock('../sync-actions-device', () => ({
    actionCreateRemote: jest.fn(),
    actionUpdateRemote: jest.fn(),
    actionCreateLocal: jest.fn(),
    actionUpdateLocal: jest.fn(),
    actionDeleteLocal: jest.fn(),
    actionUnlink: jest.fn(),
    actionConflict: jest.fn(),
    actionRename: jest.fn(),
    reportFailure: jest.fn(),
}));

function createMockDeps (overrides: Partial<SyncDeps> = {}): SyncDeps {
    const mockApiClient: ISyncApiClient = {
        startSync: jest.fn().mockResolvedValue({ sessionId: 1 }),
        prepareDeduplicate: jest.fn().mockResolvedValue({ total: 0, processed: 0, done: true }),
        checkSync: jest.fn(),
        uploadFile: jest.fn().mockResolvedValue({ success: true, songId: 1, records: [], counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } }),
        commitSync: jest.fn().mockResolvedValue({
            createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
            createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0,
            linkCount: 0, unlinkCount: 0, renameCount: 0,
            conflictCount: 0, updateTimestampCount: 0, errorCount: 0,
            committedAt: new Date(),
        }),
        completeSync: jest.fn().mockResolvedValue({
            createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
            createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0,
            linkCount: 0, unlinkCount: 0, renameCount: 0,
            conflictCount: 0, updateTimestampCount: 0, errorCount: 0,
        }),
        createPendingActions: jest.fn().mockResolvedValue({ records: [] }),
        acknowledgeAction: jest.fn().mockResolvedValue({ success: true, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } }),
        resolveConflicts: jest.fn(),
        downloadSong: jest.fn().mockResolvedValue(new Blob(['data'])),
        reportSyncError: jest.fn().mockResolvedValue({ counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 1 } }),
    };

    const mockConfig: ISyncConfig = {
        getDeviceId: jest.fn().mockReturnValue(1),
        getRepositoryPath: jest.fn().mockReturnValue('/music'),
        getMusicExtensions: jest.fn().mockReturnValue(['.mp3']),
        getExcludePatterns: jest.fn().mockReturnValue([]),
        getChunkSize: jest.fn().mockReturnValue(10),
        getLastScanTotal: jest.fn().mockResolvedValue(null),
        setLastScanTotal: jest.fn().mockResolvedValue(undefined),
        setLastSyncAt: jest.fn().mockResolvedValue(undefined),
    };

    const mockState: ISyncState = {
        get isCancelled () { return false; },
        options: {
            force: false, dryRun: false, autoConfirm: false,
            treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
        },
    };

    const mockFileOps: IFileOps = {
        fileExists: jest.fn().mockReturnValue(false),
        directoryExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        writeFile: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        moveFile: jest.fn().mockResolvedValue(undefined),
        copyFile: jest.fn().mockResolvedValue(undefined),
        readFileBase64: jest.fn().mockResolvedValue('base64'),
        getModificationTime: jest.fn().mockReturnValue(new Date('2024-01-01')),
        deleteEmptyDirectories: jest.fn().mockResolvedValue(undefined),
    };

    const mockScanner: IFileSystemScanner = jest.fn().mockResolvedValue({
        files: [],
        errors: [],
    });

    const mockKeepAwake: IKeepAwake = {
        activate: jest.fn().mockResolvedValue(undefined),
        deactivate: jest.fn(),
    };

    const mockUserPrompt: IUserPrompt = {
        promptConflictResolution: jest.fn().mockResolvedValue('upload'),
        confirmDeletion: jest.fn().mockResolvedValue(true),
    };

    return {
        apiClient: mockApiClient,
        config: mockConfig,
        state: mockState,
        scanner: mockScanner,
        fileOps: mockFileOps,
        keepAwake: mockKeepAwake,
        userPrompt: mockUserPrompt,
        ...overrides,
    } as SyncDeps;
}

function createContext (overrides: Partial<SyncContext> = {}): SyncContext {
    const result: SyncResult = {
        createRemote: 0, updateRemote: 0, createLocal: 0,
        updateLocal: 0, deleteLocal: 0, link: 0,
        unlink: 0, rename: 0, skipped: 0,
        conflict: 0, updateTimestamp: 0, error: 0,
    };
    return {
        deviceId: 1,
        repositoryPath: '/music',
        decodedRepoPath: '/music',
        sessionId: 1,
        options: {
            force: false, dryRun: false, autoConfirm: false,
            treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
        },
        result,
        processedFiles: 0,
        uploadedPaths: new Set(),
        conflictedPaths: new Set(),
        ...overrides,
    };
}

describe('sync direction', () => {
    const defaultOptions = createContext().options;
    const file = { relativePath: 'song.mp3', fullPath: '/music/song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 };

    beforeEach(() => {
        jest.clearAllMocks();
    });

    test.each(['Both', 'Up', 'Down'] as const)('startSessionPhase sends direction %s', async (direction) => {
        const deps = createMockDeps();
        const ctx = createContext({ options: { ...defaultOptions, direction } });

        await startSessionPhase(deps, ctx, [], jest.fn());

        expect(deps.apiClient.startSync).toHaveBeenCalledWith(1, expect.objectContaining({ direction }));
    });

    test('uploadPhase skips checking and uploading files in direction Down', async () => {
        const deps = createMockDeps();
        const ctx = createContext({ options: { ...defaultOptions, direction: 'Down' } });

        await uploadPhase(deps, ctx, [file], jest.fn());

        expect(deps.apiClient.checkSync).not.toHaveBeenCalled();
        expect(deps.apiClient.uploadFile).not.toHaveBeenCalled();
    });

    test('serverActionsPhase skips server actions in direction Up', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            options: { ...defaultOptions, direction: 'Up' },
            pendingActions: [{ id: 1, filePath: 'song.mp3', action: 'CreateLocal', songId: 1, data: null, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem],
        });

        await serverActionsPhase(deps, ctx, jest.fn());

        expect(deps.apiClient.createPendingActions).not.toHaveBeenCalled();
        expect(actionCreateLocal).not.toHaveBeenCalled();
    });

    test.each(['Both', 'Down'] as const)('serverActionsPhase processes server actions in direction %s', async (direction) => {
        const deps = createMockDeps();
        const ctx = createContext({ options: { ...defaultOptions, direction } });

        await serverActionsPhase(deps, ctx, jest.fn());

        expect(deps.apiClient.createPendingActions).toHaveBeenCalledWith(1, 1);
    });
});

describe('resolveConflictsPhase', () => {
    const conflictRecords: SyncRecordItem[] = [
        { id: 7, filePath: 'song.mp3', action: 'Conflict', songId: 42, data: { localModifiedAt: '2024-06-01T00:00:00Z', serverModifiedAt: '2024-06-02T00:00:00Z' }, reason: null, acknowledged: false, processedAt: '' },
    ];

    test('delegates to actionConflict', async () => {
        const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
        mockedActionConflict.mockResolvedValue({ records: [], counts: undefined });

        const deps = createMockDeps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect(mockedActionConflict).toHaveBeenCalledWith(
            deps.apiClient,
            deps.fileOps,
            deps.userPrompt,
            ctx,
            conflictRecords,
            [],
            toUpdatePaths,
            expect.any(Function)
        );
    });

    test('marks the conflict path when it is NOT in toUpdatePaths', async () => {
        const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
        mockedActionConflict.mockResolvedValue({ records: [], counts: undefined });

        const deps = createMockDeps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>(['other-song.mp3']);
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect([...ctx.conflictedPaths]).toEqual(['song.mp3']);
    });

    test('does NOT mark the conflict path when it IS in toUpdatePaths', async () => {
        const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
        const toUpdatePaths = new Set<string>(['song.mp3']);
        mockedActionConflict.mockResolvedValue({ records: [], counts: undefined });

        const deps = createMockDeps();
        const ctx = createContext();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, conflictRecords, [], toUpdatePaths, onProgress);

        expect(ctx.conflictedPaths.size).toBe(0);
    });
});

describe('resolveConflictsPhase - results', () => {
    const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
    const record = (id: number, filePath: string, action: SyncRecordItem['action']): SyncRecordItem =>
        ({ id, filePath, action, songId: 1, data: null, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem);

    beforeEach(() => {
        jest.clearAllMocks();
    });

    test('adds the resolve counts to the result once', async () => {
        mockedActionConflict.mockResolvedValue({
            records: [record(11, 'song.mp3', 'UpdateTimestamp')],
            counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 1, errorCount: 0 },
        });
        const ctx = createContext();

        await resolveConflictsPhase(createMockDeps(), ctx, [record(1, 'song.mp3', 'Conflict')], [], new Set(), jest.fn());

        expect(ctx.result.updateTimestamp).toBe(1);
    });

    test('queues the device actions of the result and drops the records they supersede', async () => {
        // The checked conflict and potential update are replaced by what the server decided for them
        const conflict = record(1, 'song.mp3', 'Conflict');
        const potentialUpdate = record(2, 'other.mp3', 'UpdateLocal');
        const updateLocal = record(11, 'song.mp3', 'UpdateLocal');
        const rename = record(12, 'new/song.mp3', 'Rename');
        mockedActionConflict.mockResolvedValue({ records: [updateLocal, rename, record(13, 'other.mp3', 'UpdateTimestamp')], counts: undefined });
        const ctx = createContext({ pendingActions: [conflict, potentialUpdate, record(3, 'download.mp3', 'CreateLocal')] });

        await resolveConflictsPhase(createMockDeps(), ctx, [conflict], [potentialUpdate], new Set(), jest.fn());

        expect(ctx.pendingActions?.map(r => r.id)).toEqual([3, 11, 12]);
    });
});

describe('serverActionsPhase - progress', () => {
    const mockedActionCreateLocal = actionCreateLocal as jest.MockedFunction<typeof actionCreateLocal>;
    const mockedActionUpdateLocal = actionUpdateLocal as jest.MockedFunction<typeof actionUpdateLocal>;
    const record = (id: number, filePath: string, action: SyncRecordItem['action']): SyncRecordItem =>
        ({ id, filePath, action, songId: id, data: null, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem);
    const counts = (delta: object) => ({ createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0, ...delta });

    test('enters the server phase at 0 of every action, then reports each file and the counts', async () => {
        // The upload phase left its own label and a full bar; the server has a download and an update pending
        jest.clearAllMocks();
        const deps = createMockDeps();
        (deps.apiClient.createPendingActions as jest.Mock).mockResolvedValue({
            records: [record(1, 'new.mp3', 'CreateLocal'), record(2, 'changed.mp3', 'UpdateLocal')],
        });
        mockedActionCreateLocal.mockResolvedValue({ action: 'CreateLocal', filePath: 'new.mp3', source: 'Server', counts: counts({ createLocalCount: 1 }) });
        mockedActionUpdateLocal.mockResolvedValue({ action: 'UpdateLocal', filePath: 'changed.mp3', source: 'Server', counts: counts({ updateLocalCount: 1 }) });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, createContext(), onProgress);

        const reports = onProgress.mock.calls.map(([p]) => p);
        expect(reports[0]).toEqual({ phase: 'server', currentFile: '' });
        expect(reports[1]).toEqual({ phase: 'server', totalFiles: 2, processedFiles: 0, currentFile: '' });
        expect(reports.slice(2).map(p => p.currentFile).filter(Boolean)).toEqual(['new.mp3', 'changed.mp3']);
        expect(reports.filter(p => p.processedFiles > 0).map(p => [p.processedFiles, p.totalFiles, p.createLocal, p.updateLocal]))
            .toEqual([[1, 2, 1, 0], [2, 2, 1, 1]]);
    });
});

describe('completePhase', () => {
    test('authoritative server counts override client estimates', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                completeSync: jest.fn().mockResolvedValue({
                    createRemoteCount: 10,
                    updateRemoteCount: 5,
                    skippedCount: 20,
                    createLocalCount: 3,
                    updateLocalCount: 0,
                    deleteLocalCount: 2,
                    linkCount: 0,
                    unlinkCount: 0,
                    renameCount: 0,
                    conflictCount: 0,
                    updateTimestampCount: 0,
                    errorCount: 1,
                }),
            },
        });
        const ctx = createContext();
        ctx.result.createRemote = 8;
        ctx.result.updateRemote = 4;
        const onProgress = jest.fn();

        await completePhase(deps, ctx, 30, onProgress);

        expect(ctx.result.createRemote).toBe(10);
        expect(ctx.result.updateRemote).toBe(5);
        expect(ctx.result.skipped).toBe(20);
        expect(ctx.result.createLocal).toBe(3);
        expect(ctx.result.deleteLocal).toBe(2);
        expect(ctx.result.error).toBe(1);
    });

    test('saves lastSyncAt and lastScanTotal', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                completeSync: jest.fn().mockResolvedValue({
                    createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
                    createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0,
                    linkCount: 0, unlinkCount: 0, renameCount: 0,
                    conflictCount: 0, updateTimestampCount: 0, errorCount: 0,
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        await completePhase(deps, ctx, 50, onProgress);

        expect(deps.config.setLastSyncAt).toHaveBeenCalledWith(expect.any(String));
        expect(deps.config.setLastScanTotal).toHaveBeenCalledWith(50);
    });

    test('reports completing phase', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                completeSync: jest.fn().mockResolvedValue({
                    createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
                    createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0,
                    linkCount: 0, unlinkCount: 0, renameCount: 0,
                    conflictCount: 0, updateTimestampCount: 0, errorCount: 0,
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        await completePhase(deps, ctx, 0, onProgress);

        expect(onProgress).toHaveBeenCalledWith({ phase: 'completing' });
    });
});

describe('prepareDeduplicatePhase', () => {
    test.each([
        { deduplicate: false, direction: 'Both' as const },
        { deduplicate: true, direction: 'Down' as const },
    ])('is skipped without upload deduplication (deduplicate: $deduplicate, direction: $direction)', async ({ deduplicate, direction }) => {
        const deps = createMockDeps();
        const ctx = createContext({ options: { ...createContext().options, deduplicate, direction } });

        await prepareDeduplicatePhase(deps, ctx, jest.fn());

        expect(deps.apiClient.prepareDeduplicate).not.toHaveBeenCalled();
    });

    test.each(['Both' as const, 'Up' as const])('prepares until done, reporting progress (direction: %s)', async (direction) => {
        const deps = createMockDeps();
        (deps.apiClient.prepareDeduplicate as jest.Mock)
            .mockResolvedValueOnce({ total: 45, processed: 20, done: false })
            .mockResolvedValueOnce({ total: 45, processed: 40, done: false })
            .mockResolvedValueOnce({ total: 45, processed: 45, done: true });
        const ctx = createContext({ options: { ...createContext().options, deduplicate: true, direction } });
        const onProgress = jest.fn();

        await prepareDeduplicatePhase(deps, ctx, onProgress);

        expect(deps.apiClient.prepareDeduplicate).toHaveBeenCalledTimes(3);
        expect(deps.apiClient.prepareDeduplicate).toHaveBeenCalledWith(1, 1);
        const reports = onProgress.mock.calls.map(([p]) => p);
        expect(reports.every(p => p.phase === 'fingerprinting')).toBe(true);
        expect(reports.filter(p => p.totalFiles > 0).map(p => p.processedFiles)).toEqual([20, 40, 45]);
    });
});

describe('uploadPhase - empty file list short-circuit', () => {
    test('early returns without calling checkSync when files list is empty', async () => {
        const deps = createMockDeps();
        const ctx = createContext();
        const onProgress = jest.fn();

        await uploadPhase(deps, ctx, [], onProgress);

        expect(deps.apiClient.checkSync).not.toHaveBeenCalled();
        expect(deps.apiClient.uploadFile).not.toHaveBeenCalled();
    });
});

describe('uploadPhase - start progress', () => {
    test('starts by reporting 0 of every file, resetting the progress of the previous phase', async () => {
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({ records: [] });
        const ctx = createContext();
        const onProgress = jest.fn();
        const files = ['a.mp3', 'b.mp3'].map(relativePath => ({ relativePath, modifiedAt: new Date(), createdAt: new Date() }));

        await uploadPhase(deps, ctx, files as never, onProgress);

        expect(onProgress).toHaveBeenNthCalledWith(1, { phase: 'upload', totalFiles: 2, processedFiles: 0, currentFile: '' });
    });
});

describe('uploadPhase - progress', () => {
    const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
    const mockedActionCreateRemote = actionCreateRemote as jest.MockedFunction<typeof actionCreateRemote>;
    const scanned = (relativePath: string) => ({ relativePath, fullPath: `/music/${relativePath}`, modifiedAt: new Date(), createdAt: new Date(), size: 1 });
    const record = (id: number, filePath: string, action: SyncRecordItem['action']): SyncRecordItem =>
        ({ id, filePath, action, songId: 1, data: null, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem);

    beforeEach(() => {
        jest.clearAllMocks();
    });

    test('advances as files are checked, resolved and uploaded, returning to the upload phase after resolving', async () => {
        // 4 files in chunks of 2: the first chunk has a potential update and an unchanged file, the second two uploads
        const deps = createMockDeps();
        (deps.config.getChunkSize as jest.Mock).mockReturnValue(2);
        (deps.apiClient.checkSync as jest.Mock)
            .mockResolvedValueOnce({ records: [record(1, 'a.mp3', 'UpdateLocal')] })
            .mockResolvedValueOnce({ records: [record(2, 'c.mp3', 'CreateRemote'), record(3, 'd.mp3', 'CreateRemote')] });
        // Resolving reports the files settled by its request through the context
        mockedActionConflict.mockImplementation(async (_api, _ops, _prompt, ctx, _conflicts, _updates, _paths, onProgress) => {
            ctx.processedFiles += 1;
            onProgress({ phase: 'resolving', processedFiles: ctx.processedFiles });
            return { records: [], counts: undefined };
        });
        mockedActionCreateRemote.mockResolvedValue({ action: 'CreateRemote', filePath: '', source: 'Device' });
        const ctx = createContext();
        const onProgress = jest.fn();

        await uploadPhase(deps, ctx, ['a.mp3', 'b.mp3', 'c.mp3', 'd.mp3'].map(scanned), onProgress);

        const reports = onProgress.mock.calls.map(([p]) => [p.phase, p.processedFiles]);
        expect(reports).toEqual([
            ['upload', 0],
            ['upload', 1], ['resolving', 2], ['upload', 2],
            ['upload', 2], ['upload', 3], ['upload', 4], ['upload', 4],
        ]);
    });

    test('counts the files of a failed check as processed', async () => {
        const deps = createMockDeps();
        (deps.config.getChunkSize as jest.Mock).mockReturnValue(2);
        (deps.apiClient.checkSync as jest.Mock)
            .mockRejectedValueOnce(new Error('boom'))
            .mockResolvedValueOnce({ records: [] });
        const ctx = createContext();
        const onProgress = jest.fn();

        await uploadPhase(deps, ctx, ['a.mp3', 'b.mp3', 'c.mp3'].map(scanned), onProgress);

        expect(onProgress.mock.calls.map(([p]) => p.processedFiles)).toEqual([0, 2, 3, 3]);
    });
});

describe('uploadPhase - uploadedPaths', () => {
    const mockedActionCreateRemote = actionCreateRemote as jest.MockedFunction<typeof actionCreateRemote>;
    const mockedActionUpdateRemote = actionUpdateRemote as jest.MockedFunction<typeof actionUpdateRemote>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateRemote.mockResolvedValue({
            action: 'CreateRemote',
            filePath: '',
            source: 'Device',
        });
        mockedActionUpdateRemote.mockResolvedValue({
            action: 'UpdateRemote',
            filePath: '',
            source: 'Device',
        });
    });

    test('uploadedPaths only contains toCreate and toUpdate paths, not skipped files', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    records: [
                        { id: 1, filePath: 'new-song.mp3', action: 'CreateRemote', songId: null, data: null, reason: null, acknowledged: false, processedAt: '' },
                        { id: 2, filePath: 'updated-song.mp3', action: 'UpdateRemote', songId: 5, data: null, reason: null, acknowledged: false, processedAt: '' },
                    ],
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        const files = [
            { relativePath: 'new-song.mp3', fullPath: '/music/new-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
            { relativePath: 'updated-song.mp3', fullPath: '/music/updated-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
            { relativePath: 'unchanged-song.mp3', fullPath: '/music/unchanged-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ];

        await uploadPhase(deps, ctx, files, onProgress);

        expect(ctx.uploadedPaths.has('new-song.mp3')).toBe(true);
        expect(ctx.uploadedPaths.has('updated-song.mp3')).toBe(true);
        expect(ctx.uploadedPaths.has('unchanged-song.mp3')).toBe(false);
    });

    test('does not call createPendingActions', async () => {
        const mockCreatePendingActions = jest.fn().mockResolvedValue({ records: [] });
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                createPendingActions: mockCreatePendingActions,
                checkSync: jest.fn().mockResolvedValue({
                    records: [],
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        await uploadPhase(deps, ctx, [], onProgress);

        expect(mockCreatePendingActions).not.toHaveBeenCalled();
    });
});

describe('uploadPhase - upload inputs', () => {
    const mockedActionCreateRemote = actionCreateRemote as jest.MockedFunction<typeof actionCreateRemote>;
    const mockedActionUpdateRemote = actionUpdateRemote as jest.MockedFunction<typeof actionUpdateRemote>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateRemote.mockResolvedValue({ action: 'Error', filePath: 'new.mp3', source: 'Device', errorMessage: 'File not found' });
        mockedActionUpdateRemote.mockResolvedValue({ action: 'UpdateRemote', filePath: 'changed.mp3', source: 'Device' });
    });

    test('uploads use the path and timestamps from the record data, like the CLI', async () => {
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({
            records: [
                { id: 1, filePath: 'new.mp3', action: 'CreateRemote', songId: null, data: { modifiedAt: '2024-02-01T00:00:00.000Z', createdAt: '2024-01-01T00:00:00.000Z' }, reason: 'New file', acknowledged: false, processedAt: '' },
                { id: 2, filePath: 'changed.mp3', action: 'UpdateRemote', songId: 5, data: { modifiedAt: '2024-04-01T00:00:00.000Z', createdAt: '2024-03-01T00:00:00.000Z' }, reason: 'Changed', acknowledged: false, processedAt: '' },
            ],
        });
        const ctx = createContext({ decodedRepoPath: '/storage/music' });
        const scanned = (relativePath: string) => ({ relativePath, fullPath: `content://scanned/${relativePath}`, modifiedAt: new Date('2030-01-01'), createdAt: new Date('2030-01-01'), size: 1 });

        await uploadPhase(deps, ctx, [scanned('new.mp3'), scanned('changed.mp3')], jest.fn());

        expect(mockedActionCreateRemote).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, {
            relativePath: 'new.mp3',
            fullPath: '/storage/music/new.mp3',
            modifiedAt: new Date('2024-02-01T00:00:00.000Z'),
            createdAt: new Date('2024-01-01T00:00:00.000Z'),
        }, 'New file');
        expect(mockedActionUpdateRemote).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, {
            relativePath: 'changed.mp3',
            fullPath: '/storage/music/changed.mp3',
            modifiedAt: new Date('2024-04-01T00:00:00.000Z'),
            createdAt: new Date('2024-03-01T00:00:00.000Z'),
        }, 'Changed');
    });

    test('an upload is attempted even when the file is not part of the checked chunk', async () => {
        // The server can name a path the chunk did not list; the upload action reports it if the file is missing
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({
            records: [{ id: 1, filePath: 'new.mp3', action: 'CreateRemote', songId: null, data: null, reason: null, acknowledged: false, processedAt: '' }],
        });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [{ relativePath: 'other.mp3', fullPath: '/music/other.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1 }], jest.fn());

        expect(mockedActionCreateRemote).toHaveBeenCalledTimes(1);
        expect(ctx.uploadedPaths.has('new.mp3')).toBe(true);
    });
});

describe('uploadPhase - accumulated pending actions from checkSync', () => {
    const mockedActionCreateRemote = actionCreateRemote as jest.MockedFunction<typeof actionCreateRemote>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateRemote.mockResolvedValue({
            action: 'CreateRemote',
            filePath: '',
            source: 'Device',
        });
    });

    test('accumulates pendingActions from checkSync responses without calling createPendingActions', async () => {
        const mockCreatePendingActions = jest.fn().mockResolvedValue({ records: [] });
        const checkSyncRecords: SyncRecordItem[] = [
            { id: 10, filePath: 'download-me.mp3', action: 'CreateLocal', songId: 5, data: { songId: 5 }, reason: 'New on server', acknowledged: false, processedAt: '' },
        ];
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                createPendingActions: mockCreatePendingActions,
                checkSync: jest.fn().mockResolvedValue({
                    records: checkSyncRecords,
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        const files = [
            { relativePath: 'some-song.mp3', fullPath: '/music/some-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ];

        await uploadPhase(deps, ctx, files, onProgress);

        expect(mockCreatePendingActions).not.toHaveBeenCalled();
        expect(ctx.pendingActions).toHaveLength(1);
        expect(ctx.pendingActions![0].id).toBe(10);
    });
});

describe('uploadPhase - device actions returned by an upload', () => {
    const mockedActionUpdateRemote = actionUpdateRemote as jest.MockedFunction<typeof actionUpdateRemote>;

    beforeEach(() => {
        jest.clearAllMocks();
    });

    test('queues an UpdateLocal returned by the upload of a previous version of a song', async () => {
        // The changed local file turns out to be a previous version of its song: the upload answers with
        // an UpdateLocal, so the device downloads the current version in this session
        const updateLocalRecord: SyncRecordItem = { id: 20, filePath: 'song.mp3', action: 'UpdateLocal', songId: 5, data: { songId: 5 }, reason: 'Server version wins', acknowledged: false, processedAt: '' };
        mockedActionUpdateRemote.mockResolvedValue({
            action: 'UpdateRemote',
            filePath: 'song.mp3',
            source: 'Device',
            records: [updateLocalRecord],
        });
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    records: [{ id: 1, filePath: 'song.mp3', action: 'UpdateRemote', songId: 5, data: null, reason: null, acknowledged: false, processedAt: '' }],
                }),
            },
        });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [
            { relativePath: 'song.mp3', fullPath: '/music/song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ], jest.fn());

        expect(ctx.pendingActions?.map(r => r.id)).toContain(20);
    });
});

describe('uploadPhase - conflictedPaths conditional tracking', () => {
    const mockedActionCreateRemote = actionCreateRemote as jest.MockedFunction<typeof actionCreateRemote>;
    const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateRemote.mockResolvedValue({
            action: 'CreateRemote',
            filePath: '',
            source: 'Device',
        });
        mockedActionConflict.mockResolvedValue({ records: [], counts: undefined });
    });

    test('marks the conflict path when it is NOT in toUpdatePaths', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    records: [{ id: 1, filePath: 'conflict-song.mp3', action: 'Conflict', songId: 99, data: null, reason: null, acknowledged: false, processedAt: '' }],
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();
        const files = [
            { relativePath: 'conflict-song.mp3', fullPath: '/music/conflict-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ];

        await uploadPhase(deps, ctx, files, onProgress);

        expect(ctx.conflictedPaths.has('conflict-song.mp3')).toBe(true);
    });

    test('does NOT mark the conflict path when it IS in toUpdatePaths', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    records: [
                        { id: 1, filePath: 'conflict-song.mp3', action: 'Conflict', songId: 99, data: null, reason: null, acknowledged: false, processedAt: '' },
                        { id: 2, filePath: 'conflict-song.mp3', action: 'UpdateRemote', songId: 99, data: null, reason: null, acknowledged: false, processedAt: '' },
                    ],
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();
        const files = [
            { relativePath: 'conflict-song.mp3', fullPath: '/music/conflict-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ];

        await uploadPhase(deps, ctx, files, onProgress);

        expect(ctx.conflictedPaths.has('conflict-song.mp3')).toBe(false);
    });

    test('does NOT mark the conflict path when the conflict resolves to an UpdateLocal', async () => {
        // The local file is a previous version of the song: the server wins, so its UpdateLocal must be
        // performed (and acknowledged) in this session instead of being skipped as conflicted
        const updateLocalRecord: SyncRecordItem = { id: 20, filePath: 'conflict-song.mp3', action: 'UpdateLocal', songId: 99, data: { songId: 99 }, reason: 'Server version wins', acknowledged: false, processedAt: '' };
        mockedActionConflict.mockResolvedValue({ records: [updateLocalRecord], counts: undefined });
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    records: [{ id: 1, filePath: 'conflict-song.mp3', action: 'Conflict', songId: 99, data: null, reason: null, acknowledged: false, processedAt: '' }],
                }),
            },
        });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [
            { relativePath: 'conflict-song.mp3', fullPath: '/music/conflict-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ], jest.fn());

        expect(ctx.conflictedPaths.has('conflict-song.mp3')).toBe(false);
        expect(ctx.pendingActions?.map(r => r.id)).toContain(20);
    });
});

describe('uploadPhase - conflicted songs across chunks', () => {
    const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
    const record = (id: number, filePath: string, action: SyncRecordItem['action'], songId: number): SyncRecordItem =>
        ({ id, filePath, action, songId, data: null, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem);
    const file = (relativePath: string) => ({ relativePath, fullPath: `/music/${relativePath}`, modifiedAt: new Date(), createdAt: new Date(), size: 1000 });

    beforeEach(() => {
        jest.clearAllMocks();
    });

    test('a conflict from an earlier chunk stays marked after a later chunk is resolved', async () => {
        // Chunk 1 holds a real conflict; chunk 2 holds a file the server changed, which resolves to an UpdateLocal
        mockedActionConflict
            .mockResolvedValueOnce({ records: [record(11, 'conflict.mp3', 'Conflict', 1)], counts: undefined })
            .mockResolvedValueOnce({ records: [record(12, 'changed.mp3', 'UpdateLocal', 2)], counts: undefined });
        const deps = createMockDeps();
        (deps.config.getChunkSize as jest.Mock).mockReturnValue(1);
        (deps.apiClient.checkSync as jest.Mock)
            .mockResolvedValueOnce({ records: [record(1, 'conflict.mp3', 'Conflict', 1)] })
            .mockResolvedValueOnce({ records: [record(2, 'changed.mp3', 'UpdateLocal', 2)] });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [file('conflict.mp3'), file('changed.mp3')], jest.fn());

        // The conflict found in chunk 1 should still protect its file; the resolved update should not
        expect([...ctx.conflictedPaths]).toEqual(['conflict.mp3']);
    });

    test.each(['UpdateTimestamp', 'Skipped'] as const)('a conflict resolved to %s is not marked', async (resolvedAction) => {
        // The contents turn out to be equal (or the server skips the file), so there is nothing to protect
        mockedActionConflict.mockResolvedValue({ records: [record(11, 'song.mp3', resolvedAction, 1)], counts: undefined });
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({ records: [record(1, 'song.mp3', 'Conflict', 1)] });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [file('song.mp3')], jest.fn());

        expect(ctx.conflictedPaths.size).toBe(0);
    });

    test('a failed resolve request keeps the conflict marked', async () => {
        // actionConflict answers with no records when the resolve request fails
        mockedActionConflict.mockResolvedValue({ records: [], counts: undefined });
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({ records: [record(1, 'song.mp3', 'Conflict', 1)] });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [file('song.mp3')], jest.fn());

        // The local file should stay protected from downloads
        expect(ctx.conflictedPaths.has('song.mp3')).toBe(true);
    });

    test('a potential update the server could not resolve is marked', async () => {
        // The server answered the potential update with an Error, so its song must not be downloaded over
        mockedActionConflict.mockResolvedValue({ records: [record(11, 'song.mp3', 'Error', 1)], counts: undefined });
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({ records: [record(1, 'song.mp3', 'UpdateLocal', 1)] });
        const ctx = createContext();

        await uploadPhase(deps, ctx, [file('song.mp3')], jest.fn());

        expect(ctx.conflictedPaths.has('song.mp3')).toBe(true);
    });
});

describe('uploadPhase - conflicted path of a song linked at two paths', () => {
    const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;

    test.each(['Conflict', 'Error'] as const)('a conflict resolved to %s marks its path only', async (resolvedAction) => {
        mockedActionConflict.mockResolvedValue({
            records: [{ id: 11, filePath: 'song.mp3', action: resolvedAction, songId: 1, data: null, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem],
            counts: undefined,
        });
        const deps = createMockDeps();
        (deps.apiClient.checkSync as jest.Mock).mockResolvedValue({
            records: [{ id: 1, filePath: 'song.mp3', action: 'Conflict', songId: 1, data: null, reason: null, acknowledged: false, processedAt: '' }],
        });
        const ctx = createContext();
        const file = (relativePath: string) => ({ relativePath, fullPath: `/music/${relativePath}`, modifiedAt: new Date(), createdAt: new Date(), size: 1000 });

        await uploadPhase(deps, ctx, [file('song.mp3'), file('copy.mp3')], jest.fn());

        expect([...ctx.conflictedPaths]).toEqual(['song.mp3']);
    });
});

describe('serverActionsPhase - conflicted paths', () => {
    const mockedActionCreateLocal = actionCreateLocal as jest.MockedFunction<typeof actionCreateLocal>;
    const mockedActionUpdateLocal = actionUpdateLocal as jest.MockedFunction<typeof actionUpdateLocal>;
    const mockedActionRename = actionRename as jest.MockedFunction<typeof actionRename>;
    const mockedReportFailure = reportFailure as jest.MockedFunction<typeof reportFailure>;
    const record = (id: number, filePath: string, action: SyncRecordItem['action'], data: SyncRecordItem['data'] = null): SyncRecordItem =>
        ({ id, filePath, action, songId: 1, data, reason: null, acknowledged: false, processedAt: '' } as SyncRecordItem);

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateLocal.mockResolvedValue({ action: 'CreateLocal', filePath: '', source: 'Server' });
        mockedActionUpdateLocal.mockResolvedValue({ action: 'UpdateLocal', filePath: '', source: 'Server' });
        mockedReportFailure.mockResolvedValue({ action: 'Error', filePath: '', source: 'Server', counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 1 } });
    });

    test('a download over a conflicted path is reported, not performed', async () => {
        // The server asks to update a file whose local copy has an unresolved conflict
        const deps = createMockDeps();
        const ctx = createContext({
            conflictedPaths: new Set(['song.mp3']),
            pendingActions: [record(5, 'song.mp3', 'UpdateLocal')],
        });

        await serverActionsPhase(deps, ctx, jest.fn());

        // The local file should not be overwritten, and the skipped record should be reported so the commit accepts it
        expect(mockedActionUpdateLocal).not.toHaveBeenCalled();
        expect(mockedReportFailure).toHaveBeenCalledWith(deps.apiClient, ctx, 5, 'song.mp3', 1, 'Unresolved conflict', 'Server-initiated update');
        expect(ctx.result.error).toBe(1);
    });

    test('a download for another path of a conflicted song still runs', async () => {
        // The song is linked at two paths and only one of them conflicts
        const deps = createMockDeps();
        const ctx = createContext({
            conflictedPaths: new Set(['song.mp3']),
            pendingActions: [record(5, 'copy.mp3', 'UpdateLocal'), record(6, 'new.mp3', 'CreateLocal')],
        });

        await serverActionsPhase(deps, ctx, jest.fn());

        expect(mockedActionUpdateLocal).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, 1, 'copy.mp3', '/music', 5, undefined, undefined);
        expect(mockedActionCreateLocal).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, 1, 'new.mp3', '/music', 6, undefined);
        expect(mockedReportFailure).not.toHaveBeenCalled();
    });

    test('a rename of a conflicted path is reported, not performed', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            conflictedPaths: new Set(['song.mp3']),
            pendingActions: [record(5, 'new/song.mp3', 'Rename', { previousPath: 'song.mp3', newPath: 'new/song.mp3' })],
        });

        await serverActionsPhase(deps, ctx, jest.fn());

        // The conflicted file should stay where it is
        expect(mockedActionRename).not.toHaveBeenCalled();
        expect(mockedReportFailure).toHaveBeenCalledWith(deps.apiClient, ctx, 5, 'new/song.mp3', 1, 'Unresolved conflict', "Rename from 'song.mp3'");
    });
});

describe('serverActionsPhase - downloads', () => {
    const mockedActionCreateLocal = actionCreateLocal as jest.MockedFunction<typeof actionCreateLocal>;
    const mockedActionUpdateLocal = actionUpdateLocal as jest.MockedFunction<typeof actionUpdateLocal>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateLocal.mockResolvedValue({ action: 'CreateLocal', filePath: 'new.mp3', source: 'Server' });
        mockedActionUpdateLocal.mockResolvedValue({ action: 'UpdateLocal', filePath: 'changed.mp3', source: 'Server' });
    });

    test('calls the create action for CreateLocal and the update action for UpdateLocal', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            pendingActions: [
                { id: 1, filePath: 'new.mp3', action: 'CreateLocal', songId: 1, data: null, reason: 'New on server', acknowledged: false, processedAt: '' } as SyncRecordItem,
                { id: 2, filePath: 'changed.mp3', action: 'UpdateLocal', songId: 2, data: null, reason: 'Changed on server', acknowledged: false, processedAt: '' } as SyncRecordItem,
            ],
        });

        await serverActionsPhase(deps, ctx, jest.fn());

        expect(mockedActionCreateLocal).toHaveBeenCalledTimes(1);
        expect(mockedActionCreateLocal).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, 1, 'new.mp3', '/music', 1, 'New on server');
        expect(mockedActionUpdateLocal).toHaveBeenCalledTimes(1);
        expect(mockedActionUpdateLocal).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, 2, 'changed.mp3', '/music', 2, 'Changed on server', undefined);
    });

    test('passes the local source of an UpdateLocal to the update action', async () => {
        // A soundalike of a file uploaded in this session is replaced by copying that file
        const deps = createMockDeps();
        const ctx = createContext({
            pendingActions: [
                { id: 3, filePath: 'copy.mp3', action: 'UpdateLocal', songId: null, data: { localSourcePath: 'first.mp3' }, reason: 'Soundalike', acknowledged: false, processedAt: '' } as SyncRecordItem,
            ],
        });

        await serverActionsPhase(deps, ctx, jest.fn());

        expect(mockedActionUpdateLocal).toHaveBeenCalledWith(deps.apiClient, deps.fileOps, ctx, null, 'copy.mp3', '/music', 3, 'Soundalike', 'first.mp3');
    });
});

describe('serverActionsPhase - Unlink actions for non-uploaded paths', () => {
    const mockedActionUnlink = actionUnlink as jest.MockedFunction<typeof actionUnlink>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionUnlink.mockResolvedValue({
            action: 'Unlink',
            filePath: 'existing-song.mp3',
            source: 'Server',
            songId: 1,
        });
    });

    test('Unlink action for non-uploaded path is processed, not skipped', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                createPendingActions: jest.fn().mockResolvedValue({
                    records: [
                        { id: 1, filePath: 'existing-song.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
                    ],
                }),
            },
        });
        const ctx = createContext({
            uploadedPaths: new Set<string>(),
            pendingActions: [
                { id: 1, filePath: 'existing-song.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockedActionUnlink).toHaveBeenCalledWith(
            deps.apiClient,
            ctx,
            'existing-song.mp3',
            1,
            1,
            'Server removed'
        );
    });

    test('Unlink action for path in uploadedPaths is skipped and acknowledged', async () => {
        const mockAcknowledge = jest.fn().mockResolvedValue({ success: true, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } });
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                acknowledgeAction: mockAcknowledge,
                createPendingActions: jest.fn().mockResolvedValue({
                    records: [
                        { id: 1, filePath: 'just-uploaded.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
                    ],
                }),
            },
        });
        const ctx = createContext({
            uploadedPaths: new Set<string>(['just-uploaded.mp3']),
            pendingActions: [
                { id: 1, filePath: 'just-uploaded.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockedActionUnlink).not.toHaveBeenCalled();
        expect(mockAcknowledge).toHaveBeenCalledWith(1, 1, { recordIds: [1] });
    });

    test('Unlink action for path NOT in uploadedPaths is NOT skipped', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                createPendingActions: jest.fn().mockResolvedValue({
                    records: [
                        { id: 1, filePath: 'existing-song.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
                    ],
                }),
            },
        });
        const ctx = createContext({
            uploadedPaths: new Set<string>(['some-other-file.mp3']),
            pendingActions: [
                { id: 1, filePath: 'existing-song.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockedActionUnlink).toHaveBeenCalledWith(
            deps.apiClient,
            ctx,
            'existing-song.mp3',
            1,
            1,
            'Server removed'
        );
    });

    test('Uploaded-path records are acknowledged during dry-run', async () => {
        const mockAcknowledge = jest.fn().mockResolvedValue({ success: true, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } });
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                acknowledgeAction: mockAcknowledge,
                createPendingActions: jest.fn().mockResolvedValue({
                    records: [
                        { id: 1, filePath: 'just-uploaded.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
                    ],
                }),
            },
        });
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
            },
            uploadedPaths: new Set<string>(['just-uploaded.mp3']),
            pendingActions: [
                { id: 1, filePath: 'just-uploaded.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockedActionUnlink).not.toHaveBeenCalled();
        expect(mockAcknowledge).toHaveBeenCalledWith(1, 1, { recordIds: [1] });
    });
});

describe('serverActionsPhase - Rename action', () => {
    const mockedActionRename = actionRename as jest.MockedFunction<typeof actionRename>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionRename.mockResolvedValue({
            action: 'Rename',
            filePath: 'renamed-song.mp3',
            source: 'Server',
            reason: "Renamed from 'original-song.mp3'",
            recordId: 5,
        });
    });

    test('Rename action with previousPath in data calls actionRename', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            pendingActions: [
                {
                    id: 5,
                    filePath: 'renamed-song.mp3',
                    action: 'Rename' as const,
                    songId: null,
                    data: { previousPath: 'original-song.mp3', newPath: 'renamed-song.mp3' },
                    reason: 'Server renamed',
                    acknowledged: false,
                    processedAt: '',
                },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockedActionRename).toHaveBeenCalledWith(
            deps.apiClient,
            deps.fileOps,
            ctx,
            'renamed-song.mp3',
            'original-song.mp3',
            '/music',
            5
        );
    });

    test('Rename action with no data is reported as an error', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            pendingActions: [
                {
                    id: 5,
                    filePath: 'renamed-song.mp3',
                    action: 'Rename' as const,
                    songId: null,
                    data: null,
                    reason: 'Server renamed',
                    acknowledged: false,
                    processedAt: '',
                },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        // Nothing should be moved, and the record should be reported so the commit accepts it
        expect(mockedActionRename).not.toHaveBeenCalled();
        expect(reportFailure).toHaveBeenCalledWith(deps.apiClient, ctx, 5, 'renamed-song.mp3', undefined, 'Missing rename data', 'Server-initiated rename');
    });

    test('Rename action with data but no previousPath is reported as an error', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            pendingActions: [
                {
                    id: 5,
                    filePath: 'renamed-song.mp3',
                    action: 'Rename' as const,
                    songId: null,
                    data: {} as RenameData,
                    reason: 'Server renamed',
                    acknowledged: false,
                    processedAt: '',
                },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        // Nothing should be moved, and the record should be reported so the commit accepts it
        expect(mockedActionRename).not.toHaveBeenCalled();
        expect(reportFailure).toHaveBeenCalledWith(deps.apiClient, ctx, 5, 'renamed-song.mp3', undefined, 'Missing rename data', 'Server-initiated rename');
    });

    test('Rename action adds result counts to context', async () => {
        mockedActionRename.mockResolvedValue({
            action: 'Rename',
            filePath: 'renamed-song.mp3',
            source: 'Server',
            reason: "Renamed from 'original-song.mp3'",
            recordId: 5,
            counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 1, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 },
        });

        const deps = createMockDeps();
        const ctx = createContext({
            pendingActions: [
                {
                    id: 5,
                    filePath: 'renamed-song.mp3',
                    action: 'Rename' as const,
                    songId: null,
                    data: { previousPath: 'original-song.mp3', newPath: 'renamed-song.mp3' },
                    reason: 'Server renamed',
                    acknowledged: false,
                    processedAt: '',
                },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(ctx.result.rename).toBe(1);
    });
});

describe('serverActionsPhase - createPendingActions call', () => {
    const mockedActionDelete = actionDeleteLocal as jest.MockedFunction<typeof actionDeleteLocal>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionDelete.mockResolvedValue({
            action: 'DeleteLocal',
            filePath: 'song-to-delete.mp3',
            source: 'Server',
            songId: 1,
        });
    });

    test('calls createPendingActions and merges results with existing pendingActions', async () => {
        const existingRecord: SyncRecordItem = { id: 1, filePath: 'existing-record.mp3', action: 'DeleteLocal', songId: 1, data: null, reason: 'Server removed', acknowledged: false, processedAt: '' };
        const newRecord: SyncRecordItem = { id: 2, filePath: 'new-record.mp3', action: 'Unlink', songId: 2, data: { songId: 2 }, reason: 'Server unlinked', acknowledged: false, processedAt: '' };

        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                createPendingActions: jest.fn().mockResolvedValue({
                    records: [newRecord],
                }),
            },
        });
        const ctx = createContext({
            uploadedPaths: new Set<string>(),
            pendingActions: [existingRecord],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(deps.apiClient.createPendingActions).toHaveBeenCalledWith(1, 1);
        expect(ctx.pendingActions).toHaveLength(2);
        expect(ctx.pendingActions!.map(r => r.id)).toContain(1);
        expect(ctx.pendingActions!.map(r => r.id)).toContain(2);
    });

    test('calls createPendingActions at the beginning of serverActionsPhase', async () => {
        const mockCreatePendingActions = jest.fn().mockResolvedValue({ records: [] });
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                createPendingActions: mockCreatePendingActions,
            },
        });
        const ctx = createContext({
            pendingActions: [],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockCreatePendingActions).toHaveBeenCalledWith(1, 1);
        expect(mockCreatePendingActions).toHaveBeenCalledTimes(1);
    });
});
