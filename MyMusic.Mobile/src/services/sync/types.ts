// @TODO: CLI has Verbose option for detailed logging output during sync. Mobile doesn't have
// this option. Consider adding verbose mode for debugging sync operations.
import type { SyncPhase, SyncProgress } from '../../stores/syncStore';
import type { ScannerType } from '../../services/scannerRegistry';
import type {SyncRecordAction, SyncRecordItem} from '../../api/types';
import type {ExclusionMatcher} from './exclusions';
import type {AdaptiveChunkSize} from './adaptive-chunk-size';

export type {SyncRecordAction, SyncRecordItem};

export type SyncDirection = 'Both' | 'Up' | 'Down';

export interface SyncContext {
    deviceId: number;
    repositoryPath: string;
    decodedRepoPath: string;
    /** Matches the paths the exclusion rules keep out of the sync: they are not scanned, and no action touches them. */
    isExcluded: ExclusionMatcher;
    sessionId?: number;
    options: {
        force: boolean;
        dryRun: boolean;
        autoConfirm: boolean;
        treatConflictsAsErrors: boolean;
        scannerType: ScannerType;
        direction: SyncDirection;
        deduplicate: boolean;
        recordSkipped: boolean;
    };
    result: SyncResult;
    /** Scanned files already checked, resolved or uploaded: the progress of the upload phase. */
    processedFiles: number;
    uploadedPaths: Set<string>;
    conflictedPaths: Set<string>;
    /** The size of the resolve requests, kept for the whole sync so what one request teaches serves the next. */
    resolveChunkSize: AdaptiveChunkSize;
    /** The answers the user gave for every remaining question of this sync, so they are not asked again. */
    rememberedAnswers: {
        deletion?: boolean;
        conflict?: ConflictResolution;
    };
    pendingActions?: SyncRecordItem[];
}

export interface SyncActionCounts {
    createRemoteCount: number;
    updateRemoteCount: number;
    skippedCount: number;
    createLocalCount: number;
    updateLocalCount: number;
    deleteLocalCount: number;
    linkCount: number;
    unlinkCount: number;
    renameCount: number;
    conflictCount: number;
    updateTimestampCount: number;
    errorCount: number;
}

function addDeltaToResult(result: SyncResult, delta: SyncActionCounts): SyncResult {
    return {
        createRemote: result.createRemote + delta.createRemoteCount,
        updateRemote: result.updateRemote + delta.updateRemoteCount,
        createLocal: result.createLocal + delta.createLocalCount,
        updateLocal: result.updateLocal + delta.updateLocalCount,
        deleteLocal: result.deleteLocal + delta.deleteLocalCount,
        link: result.link + delta.linkCount,
        unlink: result.unlink + delta.unlinkCount,
        rename: result.rename + delta.renameCount,
        skipped: result.skipped + delta.skippedCount,
        conflict: result.conflict + delta.conflictCount,
        updateTimestamp: result.updateTimestamp + delta.updateTimestampCount,
        error: result.error + delta.errorCount,
        sessionId: result.sessionId,
        cancelled: result.cancelled,
    };
}

export { addDeltaToResult };

export interface SyncResult {
    createRemote: number;
    updateRemote: number;
    createLocal: number;
    updateLocal: number;
    deleteLocal: number;
    link: number;
    unlink: number;
    rename: number;
    skipped: number;
    conflict: number;
    updateTimestamp: number;
    error: number;
    sessionId?: number;
    cancelled?: boolean;
}

export type ConflictResolution = 'upload' | 'download' | 'skip';

export interface ActionResult {
    action: SyncRecordAction;
    filePath: string;
    source: string;
    reason?: string;
    errorMessage?: string;
    songId?: number;
    recordId?: number;
    counts?: SyncActionCounts;
    // Records the server created for an uploaded file
    records?: SyncRecordItem[];
}

export interface ResolveConflictsResult {
    records: SyncRecordItem[];
    counts?: SyncActionCounts;
}

export interface SyncDeps {
    apiClient: ISyncApiClient;
    config: ISyncConfig;
    state: ISyncState;
    scanner: IFileSystemScanner;
    fileOps: IFileOps;
    keepAwake: IKeepAwake;
    userPrompt: IUserPrompt;
}

export type SyncErrorHandler = (error: Error) => void;

export type ProgressHandler = (progress: Partial<SyncProgress>) => void;

export interface SyncFileInfo {
    relativePath: string;
    fullPath: string;
    modifiedAt: Date;
    createdAt: Date;
    size: number;
}

export interface SyncFileBase {
    relativePath: string;
    fullPath: string;
    modifiedAt: Date;
    createdAt: Date;
}

export interface ScanError {
    path: string;
    error: string;
}


/** The device options the app has a setting for. */
export interface DeviceOptions {
    icon: string | null;
    namingTemplate: string | null;
    importOnPurchase: boolean;
}

export interface ISyncApiClient {
    getDevice: (deviceId: number) => Promise<{
        device: { icon: string | null; color: string | null; namingTemplate: string | null; importOnPurchase: boolean };
    }>;

    updateDevice: (
        deviceId: number,
        request: { icon?: string; color?: string; namingTemplate?: string; importOnPurchase?: boolean }
    ) => Promise<unknown>;

    startSync: (
        deviceId: number,
        request: { dryRun?: boolean; direction?: SyncDirection; repositoryPath?: string; deduplicate?: boolean; recordSkipped?: boolean; scanErrors?: Array<{ path: string; error: string }>; deviceOptions?: { namingTemplate: string | null } }
    ) => Promise<{ sessionId: number }>;

    prepareDeduplicate: (
        deviceId: number,
        sessionId: number
    ) => Promise<{ total: number; processed: number; done: boolean }>;

    checkSync: (
        deviceId: number,
        sessionId: number,
        request: {
            files: Array<{
                path: string;
                modifiedAt: string;
                createdAt: string;
                reason?: string;
            }>;
            force: boolean;
        }
    ) => Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }>;

    uploadFile: (
        deviceId: number,
        sessionId: number,
        file: { uri: string; name: string },
        path: string,
        modifiedAt: string,
        createdAt: string,
        /** The Conflict record the user resolved by keeping this local file. */
        resolvesConflictRecordId?: number
    ) => Promise<{ success: boolean; songId: number | null; records: SyncRecordItem[]; counts: SyncActionCounts }>;

    commitSync: (
        deviceId: number,
        sessionId: number
    ) => Promise<{
        createRemoteCount: number;
        updateRemoteCount: number;
        skippedCount: number;
        createLocalCount: number;
        updateLocalCount: number;
        deleteLocalCount: number;
        linkCount: number;
        unlinkCount: number;
        renameCount: number;
        conflictCount: number;
        updateTimestampCount: number;
        errorCount: number;
        committedAt: Date;
    }>;

    completeSync: (
        deviceId: number,
        sessionId: number
    ) => Promise<{
        createRemoteCount: number;
        updateRemoteCount: number;
        skippedCount: number;
        createLocalCount: number;
        updateLocalCount: number;
        deleteLocalCount: number;
        linkCount: number;
        unlinkCount: number;
        renameCount: number;
        conflictCount: number;
        updateTimestampCount: number;
        errorCount: number;
    }>;

    createPendingActions: (
        deviceId: number,
        sessionId: number
    ) => Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }>;

    acknowledgeAction: (
        deviceId: number,
        sessionId: number,
        request: { recordIds: number[]; modifiedAt?: string }
    ) => Promise<{ success: boolean; counts: SyncActionCounts }>;

    resolveConflicts: (
        deviceId: number,
        sessionId: number,
        request: {
            conflicts: Array<{
                path: string;
                songId: number;
                checksum: string;
                checksumAlgorithm: string;
                localModifiedAt: string;
            }>;
            potentialUpdates: Array<{
                path: string;
                songId: number;
                checksum: string;
                checksumAlgorithm: string;
                localModifiedAt: string;
                lastSyncedAt: string;
            }>;
        }
    ) => Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }>;

    /** Resolves real conflicts with the server's version, as chosen by the user. */
    chooseConflicts: (
        deviceId: number,
        sessionId: number,
        request: { downloadRecordIds: number[] }
    ) => Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }>;

    /** Downloads the file of a song to the given local path, replacing any file already there. */
    downloadSong: (songId: number, destinationPath: string) => Promise<void>;

    reportSyncError: (deviceId: number, sessionId: number, request: { filePath: string; errorMessage: string; songId?: number | null; recordId?: number | null }) => Promise<{ counts: SyncActionCounts }>;
}

/**
 * The size of the request chunks of one step of the sync: the sync starts at `size` and, when the sizes
 * are adaptive, stays between `min` and `max`.
 */
export interface ChunkSizeRange {
    size: number;
    min: number;
    max: number;
}

/** How the sync sizes its chunked requests (see "Request Chunks" in docs/development/sync.md). */
export interface SyncChunkTuning {
    /** Whether the sizes follow the server's response times. When false, they stay at their configured size. */
    adaptive: boolean;
    /** How long a chunked request should take: the sizes grow or shrink towards it. */
    targetRequestMs: number;
    /** Files per check request. */
    check: ChunkSizeRange;
    /** Files per conflict resolution request. */
    resolve: ChunkSizeRange;
}

export interface ISyncConfig {
    getDeviceId: () => number | null;
    getDeviceOptions: () => DeviceOptions;
    getRepositoryPath: () => string;
    getMusicExtensions: () => string[];
    getExcludePatterns: () => string[];
    getChunkTuning: () => SyncChunkTuning;
    getLastScanTotal: () => Promise<number | null>;
    setLastScanTotal: (count: number) => Promise<void>;
    setLastSyncAt: (date: string) => Promise<void>;
}

export interface ISyncState {
    isCancelled: boolean;
    options: {
        force: boolean;
        dryRun: boolean;
        autoConfirm: boolean;
        treatConflictsAsErrors: boolean;
        scannerType: ScannerType;
        direction: SyncDirection;
        deduplicate: boolean;
        recordSkipped: boolean;
    };
}

export interface IFileSystemScanner {
    (directoryUri: string, options: ScannerOptions): Promise<ScannerResult>;
}

export interface ScannerOptions {
    extensions: string[];
    excludePatterns: string[];
    basePath: string;
    onProgress?: (scannedCount: number, currentDir: string) => void;
    onError?: (path: string, error: string) => void;
}

export interface ScannerResult {
    files: SyncFileInfo[];
    errors: ScanError[];
}

export interface IFileOps {
    /** The filesystem path of the configured repository folder. Throws when the folder has none. */
    resolveRepositoryPath: (repositoryPath: string) => Promise<string>;
    fileExists: (path: string) => boolean;
    directoryExists: (path: string) => boolean;
    ensureDirectory: (path: string) => Promise<void>;
    deleteFile: (path: string) => Promise<void>;
    moveFile: (fromPath: string, toPath: string) => Promise<void>;
    copyFile: (fromPath: string, toPath: string) => Promise<void>;
    /**
     * Computes the base64 checksum of a file with the named algorithm, the same way the server does.
     * Throws for an algorithm this client does not implement.
     */
    computeChecksum: (path: string, algorithm: string) => Promise<string>;
    getModificationTime: (path: string) => Date | null;
    deleteEmptyDirectories: (filePath: string, basePath: string) => Promise<void>;
}

export interface IKeepAwake {
    activate: () => Promise<void>;
    deactivate: () => void;
}

/**
 * What the user answered to a question, and whether the answer also stands for every remaining question
 * of the same kind in this sync.
 */
export interface PromptAnswer<T> {
    value: T;
    applyToAll: boolean;
}

export interface IUserPrompt {
    /**
     * Asks what to do with a real conflict. Only the given choices can be answered. The answer can be given
     * for every remaining conflict of the sync.
     */
    promptConflictResolution: (filePath: string, choices: ConflictResolution[]) => Promise<PromptAnswer<ConflictResolution>>;
    /** Asks whether to delete a local file. The answer can be given for every remaining deletion of the sync. */
    confirmDeletion: (filePath: string) => Promise<PromptAnswer<boolean>>;
}
