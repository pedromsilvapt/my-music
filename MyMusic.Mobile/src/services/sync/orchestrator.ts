import type {SyncDeps, SyncContext, SyncResult, ProgressHandler} from './types';
import {SyncCancelledError} from '../syncService';
import {scanPhase, startSessionPhase, uploadPhase, serverActionsPhase, completePhase} from './phases';

export async function orchestrateSync(
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler
): Promise<SyncResult> {
    try {
        await deps.keepAwake.activate();

        const previousScanTotal = await deps.config.getLastScanTotal();
        const scanResult = await scanPhase(deps, ctx, onProgress, previousScanTotal);

        await startSessionPhase(deps, ctx, scanResult.errors, onProgress);

        await uploadPhase(deps, ctx, scanResult.files, onProgress);

        await serverActionsPhase(deps, ctx, onProgress);

        await completePhase(deps, ctx, scanResult.files.length, onProgress);

    } catch (error) {
        if (error instanceof SyncCancelledError) {
            console.log('Sync cancelled by user');
            ctx.result.cancelled = true;
            return ctx.result;
        }
        console.error('Sync error:', error);
        ctx.result.failed++;
        throw error;
    } finally {
        deps.keepAwake.deactivate();
    }

    return ctx.result;
}