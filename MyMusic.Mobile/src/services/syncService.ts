// @TODO: CLI has --verbose option for detailed logging during sync.
// Mobile uses progress callbacks instead. Consider adding verbose mode for debugging.
import {getSessions, getSessionRecords} from '../api/sync';
import type {SyncDeps} from './sync/types';
import {
    createDefaultApiClient,
    createDefaultConfig,
    createDefaultFileOps,
    createDefaultKeepAwake,
    createDefaultScanner,
    createDefaultState,
    createDefaultUserPrompt,
} from './sync/defaults';
import {createSyncContext} from './sync/context';
import {orchestrateSync} from './sync/orchestrator';
import {type SyncProgress, useSyncStore} from '../stores/syncStore';
import {getDeviceId, getRepositoryPath} from './configService';

import {SyncCancelledError} from './sync/errors';
export {SyncCancelledError};

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

export async function runSync(
    onProgress: (progress: Partial<SyncProgress>) => void
): Promise<SyncResult> {
    const deviceId = getDeviceId();
    const repositoryPath = getRepositoryPath();

    if (!deviceId || !repositoryPath) {
        throw new Error('Device not configured');
    }

    const options = useSyncStore.getState().options;

    const deps: SyncDeps = {
        apiClient: createDefaultApiClient(),
        config: createDefaultConfig(),
        state: createDefaultState(),
        scanner: createDefaultScanner(options.scannerType),
        fileOps: createDefaultFileOps(),
        keepAwake: createDefaultKeepAwake(),
        userPrompt: createDefaultUserPrompt(),
    };

    const ctx = createSyncContext(deps.config, deps.state);

    return orchestrateSync(deps, ctx, onProgress);
}

export async function fetchSyncHistory(deviceId: number, count: number = 10) {
    return getSessions(deviceId, count);
}

export async function fetchSessionDetails(deviceId: number, sessionId: number, actions?: string, source?: string, limit?: number, offset?: number | null, sort?: string) {
    return getSessionRecords(deviceId, sessionId, actions, source, limit, offset, sort);
}