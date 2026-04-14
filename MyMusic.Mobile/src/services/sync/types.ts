import type { SyncPhase, SyncProgress } from '../../stores/syncStore';
import type { ScannerType } from '../../services/scannerRegistry';
import type { SyncAction, SyncRecordAction } from '../../api/types';

export type { SyncAction, SyncRecordAction };

export interface PendingActionItem {
    songId: number | null;
    path: string;
    action: SyncAction;
}

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
    pendingActions?: PendingActionItem[];
}

export interface SyncResult {
    created: number;
    updated: number;
    skipped: number;
    downloaded: number;
    removed: number;
    failed: number;
    conflicts: number;
    sessionId?: number;
    cancelled?: boolean;
}

export type ConflictResolution = 'upload' | 'download' | 'skip';

export interface RecordItem {
    filePath: string;
    action: SyncRecordAction;
    source: string;
    reason?: string;
    errorMessage?: string;
    songId?: number;
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

export interface SyncConflict {
    path: string;
    reason: string;
}

export interface ISyncApiClient {
    startSync: (
        deviceId: number,
        request: { dryRun?: boolean; repositoryPath?: string }
    ) => Promise<{ sessionId: number }>;

    checkSync: (
        deviceId: number,
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
        toCreate: Array<{
            path: string;
            modifiedAt: Date;
            createdAt: Date;
            reason?: string;
        }>;
        toUpdate: Array<{
            path: string;
            modifiedAt: Date;
            createdAt: Date;
            reason?: string;
        }>;
        potentialConflicts: Array<{
            path: string;
            localModifiedAt: Date;
            serverModifiedAt: Date;
            lastSyncedAt: Date | null;
            songId: number;
            serverChecksum: string;
            serverChecksumAlgorithm: string;
        }>;
        pendingActions: PendingActionItem[];
    }>;

    uploadFile: (
        deviceId: number,
        file: { uri: string; name: string },
        path: string,
        modifiedAt: string,
        createdAt: string
    ) => Promise<{ success: boolean; songId: number; pendingActions: PendingActionItem[] }>;

    recordChunk: (
        deviceId: number,
        sessionId: number,
        request: {
            records: Array<{
                filePath: string;
                action: SyncRecordAction;
                source?: string;
                songId?: number;
                errorMessage?: string;
                reason?: string;
            }>;
        }
    ) => Promise<{ success: boolean }>;

    completeSync: (
        deviceId: number,
        sessionId: number
    ) => Promise<{
        createdCount: number;
        updatedCount: number;
        skippedCount: number;
        downloadedCount: number;
        removedCount: number;
        errorCount: number;
    }>;

    getPendingActions: (
        deviceId: number
    ) => Promise<{
        actions: PendingActionItem[];
    }>;

    acknowledgeAction: (
        deviceId: number,
        request: { devicePath: string; modifiedAt?: string }
    ) => Promise<{ success: boolean }>;

    resolveConflicts: (
        deviceId: number,
        request: {
            conflicts: Array<{
                path: string;
                songId: number;
                fileContentBase64: string;
                localModifiedAt: string;
            }>;
        }
    ) => Promise<{
        toUpload: Array<{
            path: string;
            modifiedAt: Date;
            createdAt: Date;
            reason?: string;
        }>;
        resolved: Array<{
            path: string;
            modifiedAt: Date;
            createdAt: Date;
            reason?: string;
        }>;
        conflicts: SyncConflict[];
    }>;

    downloadSong: (songId: number) => Promise<Blob>;
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
    ensureDirectory: (path: string) => Promise<void>;
    writeFile: (path: string, data: Blob) => Promise<void>;
    deleteFile: (path: string) => Promise<void>;
    readFileBase64: (path: string) => Promise<string>;
    getModificationTime: (path: string) => Date | null;
}

export interface IKeepAwake {
    activate: () => Promise<void>;
    deactivate: () => void;
}

export interface IUserPrompt {
    promptConflictResolution: (filePath: string) => Promise<ConflictResolution>;
    confirmDeletion: (filePath: string) => Promise<boolean>;
}
