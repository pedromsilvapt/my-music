import { resolveConflictsPhase, completePhase, uploadPhase, serverActionsPhase } from '../phases';
import { actionCreateRemote, actionUpdateRemote, actionDelete, actionConflict, actionRename } from '../sync-actions-device';
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
    actionDelete: jest.fn(),
    actionConflict: jest.fn(),
    actionRename: jest.fn(),
}));

function createMockDeps (overrides: Partial<SyncDeps> = {}): SyncDeps {
    const mockApiClient: ISyncApiClient = {
        startSync: jest.fn().mockResolvedValue({ sessionId: 1 }),
        checkSync: jest.fn(),
        uploadFile: jest.fn().mockResolvedValue({ success: true, songId: 1, recordId: null, action: null, data: null, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } }),
        commitSync: jest.fn().mockResolvedValue({
            createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
            createLocalCount: 0, updateLocalCount: 0, deleteCount: 0,
            linkCount: 0, unlinkCount: 0, renameCount: 0,
            conflictCount: 0, updateTimestampCount: 0, errorCount: 0,
            committedAt: new Date(),
        }),
        completeSync: jest.fn().mockResolvedValue({
            createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
            createLocalCount: 0, updateLocalCount: 0, deleteCount: 0,
            linkCount: 0, unlinkCount: 0, renameCount: 0,
            conflictCount: 0, updateTimestampCount: 0, errorCount: 0,
        }),
        createPendingActions: jest.fn().mockResolvedValue({ records: [] }),
        acknowledgeAction: jest.fn().mockResolvedValue({ success: true, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } }),
        resolveConflicts: jest.fn(),
        downloadSong: jest.fn().mockResolvedValue(new Blob(['data'])),
        reportSyncError: jest.fn().mockResolvedValue({ counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 1 } }),
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
            treatConflictsAsErrors: false, scannerType: 'fileSystem',
        },
    };

    const mockFileOps: IFileOps = {
        fileExists: jest.fn().mockReturnValue(false),
        directoryExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        writeFile: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        moveFile: jest.fn().mockResolvedValue(undefined),
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
        updateLocal: 0, delete: 0, link: 0,
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
            treatConflictsAsErrors: false, scannerType: 'fileSystem',
        },
        result,
        uploadedPaths: new Set(),
        pendingDownloadPaths: new Set(),
        conflictedSongIds: new Set(),
        ...overrides,
    };
}

describe('resolveConflictsPhase', () => {
    const chunk = [
        { relativePath: 'song.mp3', fullPath: '/music/song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
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

    test('delegates to actionConflict', async () => {
        const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
        mockedActionConflict.mockResolvedValue({ conflicts: 0, toUpdatePaths: new Set() });

        const deps = createMockDeps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(mockedActionConflict).toHaveBeenCalledWith(
            deps.apiClient,
            deps.fileOps,
            deps.userPrompt,
            ctx,
            potentialConflicts,
            chunk,
            toUpdatePaths,
            expect.any(Function)
        );
    });

    test('adds songId to conflictedSongIds when conflict path is NOT in toUpdatePaths', async () => {
        const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
        mockedActionConflict.mockResolvedValue({ conflicts: 0, toUpdatePaths: new Set() });

        const deps = createMockDeps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>(['other-song.mp3']);
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.conflictedSongIds.has(42)).toBe(true);
    });

    test('does NOT add songId to conflictedSongIds when conflict path IS in toUpdatePaths', async () => {
        const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;
        const toUpdatePaths = new Set<string>(['song.mp3']);
        mockedActionConflict.mockResolvedValue({ conflicts: 0, toUpdatePaths });

        const deps = createMockDeps();
        const ctx = createContext();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.conflictedSongIds.has(42)).toBe(false);
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
                    deleteCount: 2,
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
        expect(ctx.result.delete).toBe(2);
        expect(ctx.result.error).toBe(1);
    });

    test('saves lastSyncAt and lastScanTotal', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                completeSync: jest.fn().mockResolvedValue({
                    createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0,
                    createLocalCount: 0, updateLocalCount: 0, deleteCount: 0,
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
                    createLocalCount: 0, updateLocalCount: 0, deleteCount: 0,
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
                    toCreate: [{ path: 'new-song.mp3', modifiedAt: new Date(), createdAt: new Date() }],
                    toUpdate: [{ path: 'updated-song.mp3', modifiedAt: new Date(), createdAt: new Date() }],
                    potentialConflicts: [],
                    records: [],
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
                    toCreate: [],
                    toUpdate: [],
                    potentialConflicts: [],
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
                    toCreate: [],
                    toUpdate: [],
                    potentialConflicts: [],
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
        expect(ctx.pendingDownloadPaths.has('download-me.mp3')).toBe(true);
    });
});

describe('uploadPhase - conflictedSongIds conditional tracking', () => {
    const mockedActionCreateRemote = actionCreateRemote as jest.MockedFunction<typeof actionCreateRemote>;
    const mockedActionConflict = actionConflict as jest.MockedFunction<typeof actionConflict>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionCreateRemote.mockResolvedValue({
            action: 'CreateRemote',
            filePath: '',
            source: 'Device',
        });
        mockedActionConflict.mockResolvedValue({ conflicts: 0, toUpdatePaths: new Set() });
    });

    test('adds songId to conflictedSongIds when conflict path is NOT in toUpdatePaths', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    toCreate: [],
                    toUpdate: [],
                    potentialConflicts: [{
                        path: 'conflict-song.mp3',
                        localModifiedAt: new Date('2024-06-01'),
                        serverModifiedAt: new Date('2024-06-02'),
                        lastSyncedAt: null,
                        songId: 99,
                        serverChecksum: 'abc',
                        serverChecksumAlgorithm: 'md5',
                    }],
                    records: [],
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();
        const files = [
            { relativePath: 'conflict-song.mp3', fullPath: '/music/conflict-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ];

        await uploadPhase(deps, ctx, files, onProgress);

        expect(ctx.conflictedSongIds.has(99)).toBe(true);
    });

    test('does NOT add songId to conflictedSongIds when conflict path IS in toUpdatePaths', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                checkSync: jest.fn().mockResolvedValue({
                    toCreate: [],
                    toUpdate: [{ path: 'conflict-song.mp3', modifiedAt: new Date(), createdAt: new Date() }],
                    potentialConflicts: [{
                        path: 'conflict-song.mp3',
                        localModifiedAt: new Date('2024-06-01'),
                        serverModifiedAt: new Date('2024-06-02'),
                        lastSyncedAt: null,
                        songId: 99,
                        serverChecksum: 'abc',
                        serverChecksumAlgorithm: 'md5',
                    }],
                    records: [],
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();
        const files = [
            { relativePath: 'conflict-song.mp3', fullPath: '/music/conflict-song.mp3', modifiedAt: new Date(), createdAt: new Date(), size: 1000 },
        ];

        await uploadPhase(deps, ctx, files, onProgress);

        expect(ctx.conflictedSongIds.has(99)).toBe(false);
    });
});

describe('serverActionsPhase - Unlink actions for non-uploaded paths', () => {
    const mockedActionDelete = actionDelete as jest.MockedFunction<typeof actionDelete>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionDelete.mockResolvedValue({
            action: 'Delete',
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

        expect(mockedActionDelete).toHaveBeenCalledWith(
            deps.apiClient,
            deps.fileOps,
            deps.userPrompt,
            ctx,
            'existing-song.mp3',
            '/music',
            1,
            1,
            'Server removed'
        );
    });

    test('Unlink action for path in uploadedPaths is skipped and acknowledged', async () => {
        const mockAcknowledge = jest.fn().mockResolvedValue({ success: true, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } });
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

        expect(mockedActionDelete).not.toHaveBeenCalled();
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

        expect(mockedActionDelete).toHaveBeenCalledWith(
            deps.apiClient,
            deps.fileOps,
            deps.userPrompt,
            ctx,
            'existing-song.mp3',
            '/music',
            1,
            1,
            'Server removed'
        );
    });

    test('Uploaded-path records are acknowledged during dry-run', async () => {
        const mockAcknowledge = jest.fn().mockResolvedValue({ success: true, counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 } });
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
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
            uploadedPaths: new Set<string>(['just-uploaded.mp3']),
            pendingActions: [
                { id: 1, filePath: 'just-uploaded.mp3', action: 'Unlink' as const, songId: 1, data: { songId: 1 }, reason: 'Server removed', acknowledged: false, processedAt: '' },
            ],
        });
        const onProgress = jest.fn();

        await serverActionsPhase(deps, ctx, onProgress);

        expect(mockedActionDelete).not.toHaveBeenCalled();
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

    test('Rename action with no data is skipped', async () => {
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

        expect(mockedActionRename).not.toHaveBeenCalled();
    });

    test('Rename action with data but no previousPath is skipped', async () => {
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

        expect(mockedActionRename).not.toHaveBeenCalled();
    });

    test('Rename action adds result counts to context', async () => {
        mockedActionRename.mockResolvedValue({
            action: 'Rename',
            filePath: 'renamed-song.mp3',
            source: 'Server',
            reason: "Renamed from 'original-song.mp3'",
            recordId: 5,
            counts: { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 1, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 },
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
    const mockedActionDelete = actionDelete as jest.MockedFunction<typeof actionDelete>;

    beforeEach(() => {
        jest.clearAllMocks();
        mockedActionDelete.mockResolvedValue({
            action: 'Delete',
            filePath: 'song-to-delete.mp3',
            source: 'Server',
            songId: 1,
        });
    });

    test('calls createPendingActions and merges results with existing pendingActions', async () => {
        const existingRecord: SyncRecordItem = { id: 1, filePath: 'existing-record.mp3', action: 'Delete', songId: 1, data: null, reason: 'Server removed', acknowledged: false, processedAt: '' };
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
