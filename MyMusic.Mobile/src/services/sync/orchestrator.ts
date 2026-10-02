import type {SyncDeps, SyncResult, ProgressHandler} from './types';
import {SyncCancelledError} from './errors';
import {decodeToFsPath} from '../pathUtils';
import {createEmptyResult, createSyncContext} from './context';
import {scanPhase, startSessionPhase, prepareDeduplicatePhase, uploadPhase, serverActionsPhase, commitPhase, completePhase} from './phases';

// Partial results of syncs that failed with an unexpected error, keyed by the rethrown error
const partialResults = new WeakMap<object, SyncResult>();

/**
 * Returns the partial result of a sync that `orchestrateSync` rethrew `error` for, if any.
 */
export function getPartialSyncResult(error: unknown): SyncResult | undefined {
    return typeof error === 'object' && error !== null ? partialResults.get(error) : undefined;
}

export async function orchestrateSync(
    deps: SyncDeps,
    onProgress: ProgressHandler
): Promise<SyncResult> {
    const deviceId = deps.config.getDeviceId();
    if (deviceId === null) {
        console.error('Failed to get or create device');
        return {...createEmptyResult(), error: 1};
    }

    console.log(`Using device ID: ${deviceId}`);

    const repositoryPath = deps.config.getRepositoryPath();
    if (!repositoryPath) {
        console.error('Repository path is not configured');
        return {...createEmptyResult(), error: 1};
    }

    if (!deps.fileOps.directoryExists(decodeToFsPath(repositoryPath))) {
        console.error(`Repository path does not exist: ${repositoryPath}`);
        return {...createEmptyResult(), error: 1};
    }

    const ctx = createSyncContext(deviceId, repositoryPath, deps.state);

    try {
        await deps.keepAwake.activate();

        const previousScanTotal = await deps.config.getLastScanTotal();
        const scanResult = await scanPhase(deps, ctx, onProgress, previousScanTotal);

        await startSessionPhase(deps, ctx, scanResult.errors, onProgress);

        await prepareDeduplicatePhase(deps, ctx, onProgress);

        await uploadPhase(deps, ctx, scanResult.files, onProgress);

        await serverActionsPhase(deps, ctx, onProgress);

        await commitPhase(deps, ctx, onProgress);

        await completePhase(deps, ctx, scanResult.files.length, onProgress);

    } catch (error) {
        if (error instanceof SyncCancelledError) {
            console.log('Sync cancelled by user');
            return {...ctx.result, error: ctx.result.error + 1, cancelled: true, sessionId: ctx.sessionId};
        }
        console.error('Sync error:', error);
        // Unlike the CLI, rethrow so the app can show the error; the partial result stays available
        ctx.result = {...ctx.result, error: ctx.result.error + 1, sessionId: ctx.sessionId};
        if (typeof error === 'object' && error !== null) {
            partialResults.set(error, ctx.result);
        }
        throw error;
    } finally {
        deps.keepAwake.deactivate();
    }

    return {...ctx.result, sessionId: ctx.sessionId};
}
