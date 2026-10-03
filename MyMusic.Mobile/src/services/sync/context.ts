import type {SyncContext, ISyncState, SyncResult} from './types';
import {decodeToFsPath} from '../pathUtils';

export function createEmptyResult(): SyncResult {
    return {
        createRemote: 0,
        updateRemote: 0,
        createLocal: 0,
        updateLocal: 0,
        deleteLocal: 0,
        link: 0,
        unlink: 0,
        rename: 0,
        skipped: 0,
        conflict: 0,
        updateTimestamp: 0,
        error: 0,
    };
}

export function createSyncContext(
    deviceId: number,
    repositoryPath: string,
    state: ISyncState
): SyncContext {
    return {
        deviceId,
        repositoryPath,
        decodedRepoPath: decodeToFsPath(repositoryPath),
        options: state.options,
        result: createEmptyResult(),
        processedFiles: 0,
        uploadedPaths: new Set(),
        conflictedPaths: new Set(),
    };
}
