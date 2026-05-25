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
        createRemote: 0,
        updateRemote: 0,
        createLocal: 0,
        updateLocal: 0,
        delete: 0,
        link: 0,
        unlink: 0,
        rename: 0,
        skipped: 0,
        conflict: 0,
        updateTimestamp: 0,
        error: 0,
    };

    return {
        deviceId,
        repositoryPath,
        decodedRepoPath,
        options: state.options,
        result,
        uploadedPaths: new Set(),
        pendingDownloadPaths: new Set(),
        conflictedSongIds: new Set(),
    };
}