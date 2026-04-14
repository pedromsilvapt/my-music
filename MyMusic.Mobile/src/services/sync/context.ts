import type {SyncContext, ISyncConfig, ISyncState, SyncResult} from './types';
import {decodeToFsPath} from '../pathUtils';

export function createSyncContext(
    config: ISyncConfig,
    state: ISyncState
): SyncContext {
    const deviceId = config.getDeviceId()!;
    const repositoryPath = config.getRepositoryPath();
    const decodedRepoPath = decodeToFsPath(repositoryPath);

    const result: SyncResult = {
        created: 0,
        updated: 0,
        skipped: 0,
        downloaded: 0,
        removed: 0,
        failed: 0,
        conflicts: 0,
    };

    return {
        deviceId,
        repositoryPath,
        decodedRepoPath,
        options: state.options,
        result,
        uploadedPaths: new Set(),
        pendingDownloadPaths: new Set(),
    };
}