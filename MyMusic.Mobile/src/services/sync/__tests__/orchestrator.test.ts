import {orchestrateSync, getPartialSyncResult} from '../orchestrator';
import type {SyncDeps, IFileOps, ISyncApiClient, ISyncConfig, ISyncState, IFileSystemScanner, IKeepAwake, IUserPrompt} from '../types';

jest.mock('../errors', () => ({
    SyncCancelledError: class SyncCancelledError extends Error {
        constructor() {
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
    actionConflict: jest.fn(),
}));

function createMockDeps(overrides: Partial<SyncDeps> = {}): SyncDeps {
    const mockApiClient: ISyncApiClient = {
        getDevice: jest.fn().mockResolvedValue({device: {icon: 'IconDeviceMobile', color: '#10B981', namingTemplate: null, importOnPurchase: false}}),
        updateDevice: jest.fn().mockResolvedValue({}),
        startSync: jest.fn().mockResolvedValue({sessionId: 1}),
        prepareDeduplicate: jest.fn().mockResolvedValue({total: 0, processed: 0, done: true}),
        checkSync: jest.fn().mockResolvedValue({
            toCreate: [],
            toUpdate: [],
            potentialConflicts: [],
            potentialUpdates: [],
            skippedRecordIds: [],
            counts: {createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0},
        }),
        uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1, records: [], counts: {createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0}}),
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
        createPendingActions: jest.fn().mockResolvedValue({records: []}),
        acknowledgeAction: jest.fn().mockResolvedValue({success: true, counts: {createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0}}),
        resolveConflicts: jest.fn().mockResolvedValue({
            records: [],
            counts: {createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0},
        }),
        chooseConflicts: jest.fn(),
        downloadSong: jest.fn().mockResolvedValue(undefined),
        reportSyncError: jest.fn().mockResolvedValue({counts: {createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 1}}),
    };

    const mockConfig: ISyncConfig = {
        getDeviceId: jest.fn().mockReturnValue(1),
        getDeviceOptions: jest.fn().mockReturnValue({icon: 'IconDeviceMobile', namingTemplate: null, importOnPurchase: false}),
        getRepositoryPath: jest.fn().mockReturnValue('/music'),
        getMusicExtensions: jest.fn().mockReturnValue(['.mp3']),
        getExcludePatterns: jest.fn().mockReturnValue([]),
        getChunkSize: jest.fn().mockReturnValue(10),
        getLastScanTotal: jest.fn().mockResolvedValue(null),
        setLastScanTotal: jest.fn().mockResolvedValue(undefined),
        setLastSyncAt: jest.fn().mockResolvedValue(undefined),
    };

    const mockState: ISyncState = {
        get isCancelled() { return false; },
        options: {
            force: false, dryRun: false, autoConfirm: false,
            treatConflictsAsErrors: false, scannerType: 'fileSystem', direction: 'Both', deduplicate: false,
        },
    };

    const mockScanner: IFileSystemScanner = jest.fn().mockResolvedValue({
        files: [],
        errors: [],
    });

    const mockFileOps: IFileOps = {
        resolveRepositoryPath: jest.fn(async (path: string) => path),
        fileExists: jest.fn().mockReturnValue(false),
        directoryExists: jest.fn().mockReturnValue(true),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        moveFile: jest.fn().mockResolvedValue(undefined),
        copyFile: jest.fn().mockResolvedValue(undefined),
        computeChecksum: jest.fn().mockResolvedValue('checksum'),
        getModificationTime: jest.fn().mockReturnValue(null),
        deleteEmptyDirectories: jest.fn().mockResolvedValue(undefined),
    };

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

describe('orchestrateSync', () => {
    test('full sync executes all phases in order', async () => {
        const deps = createMockDeps();
        const onProgress = jest.fn();

        const result = await orchestrateSync(deps, onProgress);

        expect(deps.keepAwake.activate).toHaveBeenCalled();
        expect(deps.scanner).toHaveBeenCalled();
        expect(deps.apiClient.startSync).toHaveBeenCalled();
        expect(deps.apiClient.createPendingActions).toHaveBeenCalled();
        expect(deps.apiClient.completeSync).toHaveBeenCalled();
        expect(deps.config.setLastSyncAt).toHaveBeenCalled();
        expect(deps.config.setLastScanTotal).toHaveBeenCalled();
        expect(result.error).toBe(0);
        expect(result.sessionId).toBe(1);
    });

    test('saves the device options before the session starts', async () => {
        const deps = createMockDeps();
        (deps.config.getDeviceOptions as jest.Mock).mockReturnValue({icon: 'IconDeviceMobile', namingTemplate: '{{ simple_label }}.mp3', importOnPurchase: false});

        await orchestrateSync(deps, jest.fn());

        const order = (fn: unknown) => (fn as jest.Mock).mock.invocationCallOrder[0];
        expect(order(deps.apiClient.updateDevice)).toBeLessThan(order(deps.apiClient.startSync));
    });

    test('deduplicate prepares the server library after the session starts and before the server actions', async () => {
        const deps = createMockDeps();
        deps.state.options.deduplicate = true;

        await orchestrateSync(deps, jest.fn());

        const order = (fn: unknown) => (fn as jest.Mock).mock.invocationCallOrder[0];
        expect(order(deps.apiClient.prepareDeduplicate)).toBeGreaterThan(order(deps.apiClient.startSync));
        expect(order(deps.apiClient.prepareDeduplicate)).toBeLessThan(order(deps.apiClient.createPendingActions));
    });

    test('cancellation returns partial result with cancelled=true', async () => {
        const {SyncCancelledError} = require('../errors');
        const deps = createMockDeps({
            scanner: jest.fn().mockImplementation(() => {
                throw new SyncCancelledError();
            }),
        });
        const onProgress = jest.fn();

        const result = await orchestrateSync(deps, onProgress);

        expect(result.cancelled).toBe(true);
        expect(deps.keepAwake.deactivate).toHaveBeenCalled();
    });

    test('cancellation counts one error and keeps the session id', async () => {
        const {SyncCancelledError} = require('../errors');
        const deps = createMockDeps();
        (deps.apiClient.createPendingActions as jest.Mock).mockImplementation(() => {
            throw new SyncCancelledError();
        });

        const result = await orchestrateSync(deps, jest.fn());

        expect(result.cancelled).toBe(true);
        expect(result.error).toBe(1);
        expect(result.sessionId).toBe(1);
    });

    test('error propagation after cleanup', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                startSync: jest.fn().mockRejectedValue(new Error('Server unreachable')),
            },
        });
        const onProgress = jest.fn();

        const error = await orchestrateSync(deps, onProgress).catch((e) => e);

        expect(error).toBeInstanceOf(Error);
        expect(error.message).toBe('Server unreachable');
        expect(getPartialSyncResult(error)?.error).toBe(1);
        expect(deps.keepAwake.deactivate).toHaveBeenCalled();
    });

    test('unexpected error after session start rethrows with the session id set', async () => {
        const deps = createMockDeps();
        (deps.apiClient.createPendingActions as jest.Mock).mockRejectedValue(new Error('boom'));

        const error = await orchestrateSync(deps, jest.fn()).catch((e) => e);

        expect(error.message).toBe('boom');
        expect(getPartialSyncResult(error)).toMatchObject({error: 1, sessionId: 1});
    });

    test('missing device id returns an error result without syncing', async () => {
        const deps = createMockDeps();
        (deps.config.getDeviceId as jest.Mock).mockReturnValue(null);

        const result = await orchestrateSync(deps, jest.fn());

        expect(result.error).toBe(1);
        expect(deps.scanner).not.toHaveBeenCalled();
        expect(deps.apiClient.startSync).not.toHaveBeenCalled();
        expect(deps.keepAwake.activate).not.toHaveBeenCalled();
    });

    test('missing repository path returns an error result without syncing', async () => {
        const deps = createMockDeps();
        (deps.config.getRepositoryPath as jest.Mock).mockReturnValue('');

        const result = await orchestrateSync(deps, jest.fn());

        expect(result.error).toBe(1);
        expect(deps.fileOps.directoryExists).not.toHaveBeenCalled();
        expect(deps.scanner).not.toHaveBeenCalled();
    });

    test('missing repository directory returns an error result without syncing', async () => {
        const deps = createMockDeps();
        (deps.fileOps.directoryExists as jest.Mock).mockReturnValue(false);

        const result = await orchestrateSync(deps, jest.fn());

        expect(result.error).toBe(1);
        expect(deps.fileOps.directoryExists).toHaveBeenCalledWith('/music');
        expect(deps.scanner).not.toHaveBeenCalled();
    });

    test('device id is checked before the repository path', async () => {
        const deps = createMockDeps();
        (deps.config.getDeviceId as jest.Mock).mockReturnValue(null);
        (deps.config.getRepositoryPath as jest.Mock).mockReturnValue('');

        const result = await orchestrateSync(deps, jest.fn());

        expect(result.error).toBe(1);
        expect(deps.config.getRepositoryPath).not.toHaveBeenCalled();
    });

    test('keepAwake.deactivate always called on success', async () => {
        const deps = createMockDeps();
        const onProgress = jest.fn();

        await orchestrateSync(deps, onProgress);

        expect(deps.keepAwake.deactivate).toHaveBeenCalledTimes(1);
    });

    test('keepAwake.deactivate always called on error', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                startSync: jest.fn().mockRejectedValue(new Error('fail')),
            },
        });
        const onProgress = jest.fn();

        try {
            await orchestrateSync(deps, onProgress);
        } catch {}

        expect(deps.keepAwake.deactivate).toHaveBeenCalledTimes(1);
    });

    test('keepAwake.deactivate always called on cancellation', async () => {
        const {SyncCancelledError} = require('../errors');
        const deps = createMockDeps({
            scanner: jest.fn().mockImplementation(() => {
                throw new SyncCancelledError();
            }),
        });
        const onProgress = jest.fn();

        await orchestrateSync(deps, onProgress);

        expect(deps.keepAwake.deactivate).toHaveBeenCalledTimes(1);
    });
});