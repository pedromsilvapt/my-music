import {resolveConflictsPhase, completePhase} from '../phases';
import type {SyncDeps, SyncContext, SyncResult, IFileOps, ISyncApiClient, ISyncConfig, ISyncState, IFileSystemScanner, IKeepAwake, IUserPrompt} from '../types';

jest.mock('../../syncService', () => ({
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
        checkSync: jest.fn(),
        uploadFile: jest.fn().mockResolvedValue({success: true, songId: 1}),
        recordChunk: jest.fn().mockResolvedValue({success: true}),
        completeSync: jest.fn().mockResolvedValue({
            createdCount: 0, updatedCount: 0, skippedCount: 0,
            downloadedCount: 0, removedCount: 0, errorCount: 0,
        }),
        getPendingActions: jest.fn().mockResolvedValue({actions: []}),
        acknowledgeAction: jest.fn().mockResolvedValue({success: true}),
        resolveConflicts: jest.fn(),
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

    const mockFileOps: IFileOps = {
        fileExists: jest.fn().mockReturnValue(false),
        ensureDirectory: jest.fn().mockResolvedValue(undefined),
        writeFile: jest.fn().mockResolvedValue(undefined),
        deleteFile: jest.fn().mockResolvedValue(undefined),
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

function createContext(overrides: Partial<SyncContext> = {}): SyncContext {
    const result: SyncResult = {
        created: 0, updated: 0, skipped: 0,
        downloaded: 0, removed: 0, failed: 0, conflicts: 0,
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
        ...overrides,
    };
}

describe('resolveConflictsPhase', () => {
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
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                resolveConflicts: jest.fn().mockResolvedValue({
                    resolved: [{path: 'song.mp3', modifiedAt: new Date(), createdAt: new Date(), reason: 'Checksum match'}],
                    toUpload: [{path: 'song.mp3', modifiedAt: new Date(), createdAt: new Date(), reason: 'Auto-resolved'}],
                    conflicts: [],
                }),
            },
        });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(toUpdatePaths.has('song.mp3')).toBe(true);
        expect(ctx.result.conflicts).toBe(0);
    });

    test('treatConflictsAsErrors increments failed without prompt', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                resolveConflicts: jest.fn().mockResolvedValue({
                    resolved: [],
                    toUpload: [],
                    conflicts: [{path: 'song.mp3', reason: 'Different checksums'}],
                }),
            },
        });
        const ctx = createContext({
            options: {
                force: false, dryRun: false, autoConfirm: false,
                treatConflictsAsErrors: true, scannerType: 'fileSystem',
            },
        });
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.result.failed).toBe(1);
        expect(ctx.result.conflicts).toBe(1);
        expect(deps.userPrompt.promptConflictResolution).not.toHaveBeenCalled();
    });

    test('user prompt for upload adds to toUpdate', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                resolveConflicts: jest.fn().mockResolvedValue({
                    resolved: [],
                    toUpload: [],
                    conflicts: [{path: 'song.mp3', reason: 'Different checksums'}],
                }),
            },
            userPrompt: {
                promptConflictResolution: jest.fn().mockResolvedValue('upload'),
                confirmDeletion: jest.fn().mockResolvedValue(true),
            },
        });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(toUpdatePaths.has('song.mp3')).toBe(true);
        expect(deps.userPrompt.promptConflictResolution).toHaveBeenCalledWith('song.mp3');
    });

    test('user prompt for skip increments failed', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                resolveConflicts: jest.fn().mockResolvedValue({
                    resolved: [],
                    toUpload: [],
                    conflicts: [{path: 'song.mp3', reason: 'Different checksums'}],
                }),
            },
            userPrompt: {
                promptConflictResolution: jest.fn().mockResolvedValue('skip'),
                confirmDeletion: jest.fn().mockResolvedValue(true),
            },
        });
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.result.failed).toBe(1);
        expect(toUpdatePaths.has('song.mp3')).toBe(false);
    });

    test('dry-run counts conflicts without resolving', async () => {
        const deps = createMockDeps();
        const ctx = createContext({
            options: {
                force: false, dryRun: true, autoConfirm: false,
                treatConflictsAsErrors: false, scannerType: 'fileSystem',
            },
        });
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, potentialConflicts, chunk, toUpdatePaths, onProgress);

        expect(ctx.result.conflicts).toBe(1);
        expect(deps.apiClient.resolveConflicts).not.toHaveBeenCalled();
        expect(deps.userPrompt.promptConflictResolution).not.toHaveBeenCalled();
    });

    test('no conflicts does nothing', async () => {
        const deps = createMockDeps();
        const ctx = createContext();
        const toUpdatePaths = new Set<string>();
        const onProgress = jest.fn();

        await resolveConflictsPhase(deps, ctx, [], chunk, toUpdatePaths, onProgress);

        expect(ctx.result.conflicts).toBe(0);
        expect(deps.apiClient.resolveConflicts).not.toHaveBeenCalled();
    });
});

describe('completePhase', () => {
    test('authoritative server counts override client estimates', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                completeSync: jest.fn().mockResolvedValue({
                    createdCount: 10,
                    updatedCount: 5,
                    skippedCount: 20,
                    downloadedCount: 3,
                    removedCount: 2,
                    errorCount: 1,
                }),
            },
        });
        const ctx = createContext();
        ctx.result.created = 8;
        ctx.result.updated = 4;
        const onProgress = jest.fn();

        await completePhase(deps, ctx, 30, onProgress);

        expect(ctx.result.created).toBe(10);
        expect(ctx.result.updated).toBe(5);
        expect(ctx.result.skipped).toBe(20);
        expect(ctx.result.downloaded).toBe(3);
        expect(ctx.result.removed).toBe(2);
        expect(ctx.result.failed).toBe(1);
    });

    test('saves lastSyncAt and lastScanTotal', async () => {
        const deps = createMockDeps({
            apiClient: {
                ...createMockDeps().apiClient,
                completeSync: jest.fn().mockResolvedValue({
                    createdCount: 0, updatedCount: 0, skippedCount: 0,
                    downloadedCount: 0, removedCount: 0, errorCount: 0,
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
                    createdCount: 0, updatedCount: 0, skippedCount: 0,
                    downloadedCount: 0, removedCount: 0, errorCount: 0,
                }),
            },
        });
        const ctx = createContext();
        const onProgress = jest.fn();

        await completePhase(deps, ctx, 0, onProgress);

        expect(onProgress).toHaveBeenCalledWith({phase: 'completing'});
    });
});