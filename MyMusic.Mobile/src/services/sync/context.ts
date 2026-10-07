import type {SyncContext, ISyncState, SyncResult} from './types';
import {createExclusionMatcher} from './exclusions';

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
    decodedRepoPath: string,
    state: ISyncState,
    excludePatterns: string[]
): SyncContext {
    return {
        deviceId,
        repositoryPath,
        decodedRepoPath,
        isExcluded: createExclusionMatcher(excludePatterns),
        options: state.options,
        result: createEmptyResult(),
        processedFiles: 0,
        uploadedPaths: new Set(),
        conflictedPaths: new Set(),
        rememberedAnswers: {},
    };
}
