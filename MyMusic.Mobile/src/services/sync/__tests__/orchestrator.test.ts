import {orchestrateSync} from '../orchestrator';
import type {SyncDeps, SyncContext, SyncResult, IFileOps, ISyncApiClient, ISyncConfig, ISyncState, IFileSystemScanner, IKeepAwake, IUserPrompt} from '../types';

jest.mock('../errors', () => ({
    SyncCancelledError: class SyncCancelledError extends Error {
        constructor() {
            super('Sync was cancelled');
            this.name = 'SyncCancelledError';
        }
    },
}));

function createMockDeps(overrides: Partial<SyncDeps> = {}): SyncDeps {
    const mockApiClient: ISyncApiClient = {
        startSync: jest.fn().mockResolvedValue({sessionId: 1}),
        checkSync: jest.fn().mockResolvedValue({
            toCreate: [],
            toUpdate: [],
            potentialConflicts: [],
        }),
        uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1}),
        recordChunk: jest.fn().mockResolvedValue({success: true}),
        completeSync: jest.fn().mockResolvedValue({
            createdCount: 0, updatedCount: 0, skippedCount: 0,
            downloadedCount: 0, removedCount: 0, errorCount: 0,
        }),
        getPendingActions: jest.fn().mockResolvedValue({actions: []}),
        acknowledgeAction: jest.fn().mockResolvedValue({success: true}),
        resolveConflicts: jest.fn().mockResolvedValue({
            resolved: [],
            toUpload: [],
            conflicts: [],
        }),
        downloadSong: jest.fn().mockResolvedValue(new Blob(['data'])),
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
        get isCancelled() { return false; },
        options: {
            force: false, dryRun: false, autoConfirm: false,
            treatConflictsAsErrors: false, scannerType: 'fileSystem',
        },
    };

    const mockScanner: IFileSystemScanner = jest.fn().mockResolvedValue({
        files: [],
        errors: [],
    });

    const mockFileOps: IFileOps = {
        fileExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        writeFile: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
        readFileBase64: jest.fn().mockResolvedValue('base64'),
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
            force: false, dryRun: false, autoConfirm: false,
            treatConflictsAsErrors: false, scannerType: 'fileSystem',
        },
        result,
        uploadedPaths: new Set(),
        pendingDownloadPaths: new Set(),
        ...overrides,
    };
}

describe('orchestrateSync', () => {
    test('full sync executes all phases in order', async () => {
        const deps = createMockDeps();
        const ctx = createContext();
        const onProgress = jest.fn();

        const result = await orchestrateSync(deps, ctx, onProgress);

        expect(deps.keepAwake.activate).toHaveBeenCalled();
        expect(deps.scanner).toHaveBeenCalled();
        expect(deps.apiClient.startSync).toHaveBeenCalled();
        expect(deps.apiClient.getPendingActions).toHaveBeenCalled();
        expect(deps.apiClient.completeSync).toHaveBeenCalled();
        expect(deps.config.setLastSyncAt).toHaveBeenCalled();
        expect(deps.config.setLastScanTotal).toHaveBeenCalled();
        expect(result).toBe(ctx.result);
    });

    test('cancellation returns partial result with cancelled=true', async () => {
        const {SyncCancelledError} = require('../errors');
        const deps = createMockDeps({
            scanner: jest.fn().mockImplementation(() => {
                throw new SyncCancelledError();
            }),
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        const result = await orchestrateSync(deps, ctx, onProgress);

        expect(result.cancelled).toBe(true);
        expect(deps.keepAwake.deactivate).toHaveBeenCalled();
    });

    test('error propagation after cleanup', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                startSync: jest.fn().mockRejectedValue(new Error('Server unreachable')),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        await expect(orchestrateSync(deps, ctx, onProgress)).rejects.toThrow('Server unreachable');
        expect(ctx.result.failed).toBe(1);
        expect(deps.keepAwake.deactivate).toHaveBeenCalled();
    });

    test('keepAwake.deactivate always called on success', async () => {
        const deps = createMockDeps();
        const ctx = createContext();
        const onProgress = jest.fn();

        await orchestrateSync(deps, ctx, onProgress);

        expect(deps.keepAwake.deactivate).toHaveBeenCalledTimes(1);
    });

    test('keepAwake.deactivate always called on error', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                startSync: jest.fn().mockRejectedValue(new Error('fail')),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        try {
            await orchestrateSync(deps, ctx, onProgress);
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
        const ctx = createContext();
        const onProgress = jest.fn();

        await orchestrateSync(deps, ctx, onProgress);

        expect(deps.keepAwake.deactivate).toHaveBeenCalledTimes(1);
    });
});