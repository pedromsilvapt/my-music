import type { SyncDeps, SyncContext, SyncFileInfo, ScanError, SyncConflict, ProgressHandler, SyncRecordItem, ActionResult } from './types';
import { SyncActionCounts, addDeltaToResult } from './types';
import type { SyncPotentialConflictItem, SyncPotentialUpdateItem, RenameData } from '../../api/types';
import { SyncCancelledError } from './errors';
import { safeToIsoString, chunkArray, formatFilePath } from './utils';
import { actionCreateRemote, actionUpdateRemote, actionCreateLocal, actionDelete, actionConflict, actionRename } from './sync-actions-device';

const EMPTY_COUNTS: SyncActionCounts = {
    createRemoteCount: 0,
    updateRemoteCount: 0,
    skippedCount: 0,
    createLocalCount: 0,
    updateLocalCount: 0,
    deleteCount: 0,
    linkCount: 0,
    unlinkCount: 0,
    renameCount: 0,
    conflictCount: 0,
    updateTimestampCount: 0,
    errorCount: 0,
};

export interface ScanPhaseResult {
    files: SyncFileInfo[];
    errors: ScanError[];
    estimatedTotal: number;
}

export async function scanPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler,
    previousScanTotal: number | null
): Promise<ScanPhaseResult> {
    const estimatedTotal = previousScanTotal || 0;

    onProgress({
        phase: 'scanning',
        totalFiles: 0,
        estimatedTotalFiles: estimatedTotal,
        processedFiles: 0,
        scannedFiles: 0,
        currentFile: 'Scanning your music folder...',
    });

    const scanErrors: ScanError[] = [];
    let currentEstimate = estimatedTotal;

    const { files, errors } = await deps.scanner(ctx.repositoryPath, {
        extensions: deps.config.getMusicExtensions(),
        excludePatterns: deps.config.getExcludePatterns(),
        basePath: ctx.repositoryPath,
        onProgress: (scannedCount, currentDir) => {
            if (deps.state.isCancelled) {
                throw new SyncCancelledError();
            }
            const dirName = currentDir.split('/').pop() || 'music folder';
            if (scannedCount > currentEstimate) {
                currentEstimate = scannedCount;
            }
            onProgress({
                scannedFiles: scannedCount,
                estimatedTotalFiles: currentEstimate,
                currentFile: `${scannedCount} files found in ${dirName}...`,
            });
        },
        onError: (path, error) => {
            scanErrors.push({ path, error });
        },
    });

    ctx.result.error += errors.length;

    if (deps.state.isCancelled) {
        throw new SyncCancelledError();
    }

    if (files.length > currentEstimate) {
        currentEstimate = files.length;
    }

    onProgress({
        totalFiles: files.length,
        estimatedTotalFiles: currentEstimate,
        scannedFiles: files.length,
        phase: 'upload',
        currentFile: '',
    });

    return { files, errors: scanErrors, estimatedTotal: currentEstimate };
}

export async function startSessionPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    scanErrors: ScanError[],
    onProgress: ProgressHandler
): Promise<void> {
    const startResponse = await deps.apiClient.startSync(ctx.deviceId, {
        dryRun: ctx.options.dryRun,
        repositoryPath: ctx.repositoryPath,
        scanErrors: scanErrors.map(e => ({ path: e.path, error: e.error })),
    });
    ctx.sessionId = startResponse.sessionId;

    onProgress({ phase: 'server' });
}

export async function resolveConflictsPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    potentialConflicts: SyncPotentialConflictItem[],
    potentialUpdates: SyncPotentialUpdateItem[],
    chunk: SyncFileInfo[],
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<void> {
    await actionConflict(
        deps.apiClient,
        deps.fileOps,
        deps.userPrompt,
        ctx,
        potentialConflicts,
        potentialUpdates,
        chunk,
        toUpdatePaths,
        (progress) => {
            onProgress(progress);
        }
    );

    for (const conflict of potentialConflicts) {
        if (conflict.songId != null && !toUpdatePaths.has(conflict.path)) {
            ctx.conflictedSongIds.add(conflict.songId);
        }
    }
}

export async function uploadPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    files: SyncFileInfo[],
    onProgress: ProgressHandler
): Promise<void> {
    if (files.length === 0) {
        return;
    }

    const chunkSize = deps.config.getChunkSize();
    const chunks = chunkArray(files, chunkSize);

    for (let i = 0; i < chunks.length; i++) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }
        const chunk = chunks[i];

        const syncFiles = chunk.map(f => ({
            path: f.relativePath,
            modifiedAt: safeToIsoString(f.modifiedAt)!,
            createdAt: safeToIsoString(f.createdAt)!,
        }));

        let syncResponse;
        try {
            syncResponse = await deps.apiClient.checkSync(ctx.deviceId, ctx.sessionId!, {
                files: syncFiles,
                force: ctx.options.force,
            });
        } catch (e) {
            ctx.result.error += chunk.length;
            onProgress({
                phase: 'upload',
                errorMessage: `Chunk ${i + 1} failed: ${e instanceof Error ? e.message : String(e)}`,
            });
            continue;
        }

        ctx.result = addDeltaToResult(ctx.result, syncResponse.counts ?? EMPTY_COUNTS);

        if (syncResponse.records.length > 0) {
            ctx.pendingActions = mergePendingActions(ctx.pendingActions ?? [], syncResponse.records);
            ctx.pendingDownloadPaths = extractPendingDownloadPaths(ctx.pendingActions);
        }

        const toCreatePaths = new Set(syncResponse.toCreate.map(f => f.path));
        const toUpdatePaths = new Set(syncResponse.toUpdate.map(f => f.path));

        if (syncResponse.potentialConflicts.length > 0 || syncResponse.potentialUpdates.length > 0) {
            await actionConflict(
                deps.apiClient,
                deps.fileOps,
                deps.userPrompt,
                ctx,
                syncResponse.potentialConflicts,
                syncResponse.potentialUpdates,
                chunk,
                toUpdatePaths,
                (progress) => {
                    onProgress({
                        phase: progress.phase ?? 'resolving',
                        currentFile: progress.currentFile,
                        conflict: progress.conflict,
                    });
                }
            );

            for (const conflict of syncResponse.potentialConflicts) {
                if (conflict.songId != null && !toUpdatePaths.has(conflict.path)) {
                    ctx.conflictedSongIds.add(conflict.songId);
                }
            }
        }

        await processChunkUploads(deps, ctx, chunk, syncResponse.toCreate, syncResponse.toUpdate, toUpdatePaths, onProgress);
    }
}

export async function serverActionsPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler
): Promise<ActionResult[]> {
    if (deps.state.isCancelled) {
        throw new SyncCancelledError();
    }

    const pendingActionsResponse = await deps.apiClient.createPendingActions(ctx.deviceId, ctx.sessionId!);
    ctx.pendingActions = mergePendingActions(ctx.pendingActions ?? [], pendingActionsResponse.records);
    ctx.pendingDownloadPaths = extractPendingDownloadPaths(ctx.pendingActions);

    const pendingActions = ctx.pendingActions;
    const serverResults: ActionResult[] = [];

    for (const record of pendingActions) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        if (ctx.uploadedPaths.has(record.filePath) && record.action !== 'CreateLocal' && record.action !== 'UpdateLocal') {
            await deps.apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
                recordIds: [record.id],
            });
            continue;
        }

        if ((record.action === 'CreateLocal' || record.action === 'UpdateLocal')) {
            if (record.songId != null && ctx.conflictedSongIds.has(record.songId)) {
                continue;
            }

            const result = await actionCreateLocal(
                deps.apiClient,
                deps.fileOps,
                deps.userPrompt,
                ctx,
                record.songId,
                record.filePath,
                ctx.decodedRepoPath,
                record.id,
                record.reason ?? undefined
            );
            if (result) {
                serverResults.push(result);
                if (result.counts) {
                    ctx.result = addDeltaToResult(ctx.result, result.counts);
                }
            }
        } else if (record.action === 'Unlink' || record.action === 'Delete') {
            const result = await actionDelete(
                deps.apiClient,
                deps.fileOps,
                deps.userPrompt,
                ctx,
                record.filePath,
                ctx.decodedRepoPath,
                record.songId ?? undefined,
                record.id,
                record.reason ?? undefined
            );
            if (result) {
                serverResults.push(result);
                if (result.counts) {
                    ctx.result = addDeltaToResult(ctx.result, result.counts);
                }
            }
        } else if (record.action === 'Rename') {
            const renameData = record.data;
            if (renameData?.previousPath) {
                const result = await actionRename(
                    deps.apiClient,
                    deps.fileOps,
                    ctx,
                    record.filePath,
                    renameData.previousPath,
                    ctx.decodedRepoPath,
                    record.id
                );
                if (result) {
                    serverResults.push(result);
                    if (result.counts) {
                        ctx.result = addDeltaToResult(ctx.result, result.counts);
                    }
                }
            }
        }

        onProgress({
            createLocal: ctx.result.createLocal,
            delete: ctx.result.delete,
            processedFiles: pendingActions.indexOf(record) + 1,
            totalFiles: pendingActions.length,
        });
    }

    return serverResults;
}

export async function commitPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler
): Promise<void> {
    if (deps.state.isCancelled) {
        throw new SyncCancelledError();
    }

    onProgress({ phase: 'committing' });

    const commitResponse = await deps.apiClient.commitSync(ctx.deviceId, ctx.sessionId!);

    ctx.result.createRemote = commitResponse.createRemoteCount;
    ctx.result.updateRemote = commitResponse.updateRemoteCount;
    ctx.result.skipped = commitResponse.skippedCount;
    ctx.result.createLocal = commitResponse.createLocalCount;
    ctx.result.updateLocal = commitResponse.updateLocalCount;
    ctx.result.delete = commitResponse.deleteCount;
    ctx.result.link = commitResponse.linkCount;
    ctx.result.unlink = commitResponse.unlinkCount;
    ctx.result.rename = commitResponse.renameCount;
    ctx.result.conflict = commitResponse.conflictCount;
    ctx.result.updateTimestamp = commitResponse.updateTimestampCount;
    ctx.result.error = commitResponse.errorCount;
}

export async function completePhase (
    deps: SyncDeps,
    ctx: SyncContext,
    filesCount: number,
    onProgress: ProgressHandler
): Promise<void> {
    onProgress({ phase: 'completing' });

    const completeResponse = await deps.apiClient.completeSync(ctx.deviceId, ctx.sessionId!);

    ctx.result.createRemote = completeResponse.createRemoteCount;
    ctx.result.updateRemote = completeResponse.updateRemoteCount;
    ctx.result.skipped = completeResponse.skippedCount;
    ctx.result.createLocal = completeResponse.createLocalCount;
    ctx.result.updateLocal = completeResponse.updateLocalCount;
    ctx.result.delete = completeResponse.deleteCount;
    ctx.result.link = completeResponse.linkCount;
    ctx.result.unlink = completeResponse.unlinkCount;
    ctx.result.rename = completeResponse.renameCount;
    ctx.result.conflict = completeResponse.conflictCount;
    ctx.result.updateTimestamp = completeResponse.updateTimestampCount;
    ctx.result.error = completeResponse.errorCount;
    ctx.result.sessionId = ctx.sessionId;

    await deps.config.setLastSyncAt(new Date().toISOString());
    await deps.config.setLastScanTotal(filesCount);
}

async function processChunkUploads (
    deps: SyncDeps,
    ctx: SyncContext,
    chunk: SyncFileInfo[],
    toCreate: Array<{ path: string; modifiedAt: Date; createdAt: Date; reason?: string; }>,
    toUpdate: Array<{ path: string; modifiedAt: Date; createdAt: Date; reason?: string; }>,
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<void> {
    for (const fileToCreate of toCreate) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        const file = chunk.find(f => f.relativePath === fileToCreate.path);
        if (file) {
            const result = await actionCreateRemote(deps.apiClient, deps.fileOps, ctx, file, fileToCreate.reason);
            if (result.counts) {
                ctx.result = addDeltaToResult(ctx.result, result.counts);
            }
        }
        ctx.uploadedPaths.add(fileToCreate.path);

        onProgress({
            processedFiles: ctx.result.createRemote + ctx.result.updateRemote + ctx.result.skipped + ctx.result.error + ctx.result.conflict,
            currentFile: formatFilePath(fileToCreate.path, ctx.repositoryPath),
            createRemote: ctx.result.createRemote,
            updateRemote: ctx.result.updateRemote,
            skipped: ctx.result.skipped,
            error: ctx.result.error,
            conflict: ctx.result.conflict,
        });
    }

    for (const fileToUpdate of toUpdate) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        if (!toUpdatePaths.has(fileToUpdate.path)) {
            continue;
        }

        const file = chunk.find(f => f.relativePath === fileToUpdate.path);
        if (file) {
            const result = await actionUpdateRemote(deps.apiClient, deps.fileOps, ctx, file, fileToUpdate.reason);
            if (result.counts) {
                ctx.result = addDeltaToResult(ctx.result, result.counts);
            }
        }
        ctx.uploadedPaths.add(fileToUpdate.path);

        onProgress({
            processedFiles: ctx.result.createRemote + ctx.result.updateRemote + ctx.result.skipped + ctx.result.error + ctx.result.conflict,
            currentFile: formatFilePath(fileToUpdate.path, ctx.repositoryPath),
            createRemote: ctx.result.createRemote,
            updateRemote: ctx.result.updateRemote,
            skipped: ctx.result.skipped,
            error: ctx.result.error,
            conflict: ctx.result.conflict,
        });
    }
}

function extractPendingDownloadPaths (records: SyncRecordItem[]): Set<string> {
    const paths = new Set<string>();
    for (const record of records) {
        if (record.action === 'CreateLocal' || record.action === 'UpdateLocal') {
            paths.add(record.filePath);
        }
    }
    return paths;
}

function mergePendingActions (
    existing: SyncRecordItem[],
    incoming: SyncRecordItem[]
): SyncRecordItem[] {
    const merged = new Map<number, SyncRecordItem>();
    for (const record of existing) {
        merged.set(record.id, record);
    }
    for (const record of incoming) {
        merged.set(record.id, record);
    }
    return Array.from(merged.values());
}
