// @TODO: Mobile has treatConflictsAsErrors option, but CLI uses checksum-based conflict resolution.
// CLI sends file content to server for checksum comparison to auto-resolve conflicts.
// Consider aligning conflict resolution strategies between platforms.
//
// @TODO: CLI has Verbose option for detailed logging output during sync. Mobile doesn't have
// this option. Consider adding verbose mode for debugging sync operations.
import type { SyncPhase, SyncProgress } from '../../stores/syncStore';
import type { ScannerType } from '../../services/scannerRegistry';
import type {SyncRecordAction, SyncRecordItem} from '../../api/types';

export type {SyncRecordAction, SyncRecordItem};

export interface SyncContext {
    deviceId: number;
    repositoryPath: string;
    decodedRepoPath: string;
    sessionId?: number;
    options: {
        force: boolean;
        dryRun: boolean;
        autoConfirm: boolean;
        treatConflictsAsErrors: boolean;
        scannerType: ScannerType;
    };
    result: SyncResult;
    uploadedPaths: Set<string>;
    pendingDownloadPaths: Set<string>;
    conflictedSongIds: Set<number>;
    pendingActions?: SyncRecordItem[];
}

export interface SyncActionCounts {
    createRemoteCount: number;
    updateRemoteCount: number;
    skippedCount: number;
    createLocalCount: number;
    updateLocalCount: number;
    deleteCount: number;
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
        delete: result.delete + delta.deleteCount,
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
    delete: number;
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


export interface ISyncApiClient {
    startSync: (
        deviceId: number,
        request: { dryRun?: boolean; repositoryPath?: string; scanErrors?: Array<{ path: string; error: string }> }
    ) => Promise<{ sessionId: number }>;

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
        createdAt: string
    ) => Promise<{ success: boolean; songId: number | null; recordId: number | null; action: string | null; data: SyncRecordItem['data']; counts: SyncActionCounts }>;

    commitSync: (
        deviceId: number,
        sessionId: number,
        request?: { direction?: string }
    ) => Promise<{
        createRemoteCount: number;
        updateRemoteCount: number;
        skippedCount: number;
        createLocalCount: number;
        updateLocalCount: number;
        deleteCount: number;
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
        deleteCount: number;
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
                songId: number | null;
                fileContentBase64: string;
                localModifiedAt: string;
            }>;
            potentialUpdates: Array<{
                path: string;
                songId: number;
                fileContentBase64: string;
                localModifiedAt: string;
                lastSyncedAt: string;
            }>;
        }
    ) => Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }>;

    downloadSong: (songId: number) => Promise<Blob>;

    reportSyncError: (deviceId: number, sessionId: number, request: { filePath: string; errorMessage: string; songId?: number | null }) => Promise<{ counts: SyncActionCounts }>;
}

export interface ISyncConfig {
    getDeviceId: () => number | null;
    getRepositoryPath: () => string;
    getMusicExtensions: () => string[];
    getExcludePatterns: () => string[];
    getChunkSize: () => number;
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
    fileExists: (path: string) => boolean;
    directoryExists: (path: string) => boolean;
    ensureDirectory: (path: string) => Promise<void>;
    writeFile: (path: string, data: Blob) => Promise<void>;
    deleteFile: (path: string) => Promise<void>;
    moveFile: (fromPath: string, toPath: string) => Promise<void>;
    readFileBase64: (path: string) => Promise<string>;
    getModificationTime: (path: string) => Date | null;
    deleteEmptyDirectories: (filePath: string, basePath: string) => Promise<void>;
}

export interface IKeepAwake {
    activate: () => Promise<void>;
    deactivate: () => void;
}

export interface IUserPrompt {
    promptConflictResolution: (filePath: string) => Promise<ConflictResolution>;
    confirmDeletion: (filePath: string) => Promise<boolean>;
}
