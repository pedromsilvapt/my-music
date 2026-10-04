import type { SyncDeps, SyncContext, SyncFileInfo, SyncFileBase, ScanError, ProgressHandler, SyncRecordItem, ActionResult } from './types';
import { SyncActionCounts, addDeltaToResult } from './types';
import type { RenameData } from '../../api/types';
import { SyncCancelledError } from './errors';
import { safeToIsoString, chunkArray, formatFilePath } from './utils';
import { saveDeviceOptions } from './device-options';
import { actionCreateRemote, actionUpdateRemote, actionCreateLocal, actionUpdateLocal, actionDeleteLocal, actionUnlink, actionConflict, actionRename, reportFailure } from './sync-actions-device';

const EMPTY_COUNTS: SyncActionCounts = {
    createRemoteCount: 0,
    updateRemoteCount: 0,
    skippedCount: 0,
    createLocalCount: 0,
    updateLocalCount: 0,
    deleteLocalCount: 0,
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

/**
 * Saves the local device options (naming template, icon, ...) to the server before a real sync, so the
 * session runs with them. A dry run must leave the server untouched: it sends them with the session
 * instead (see `startSessionPhase`).
 */
export async function saveDeviceOptionsPhase (
    deps: SyncDeps,
    ctx: SyncContext
): Promise<void> {
    if (ctx.options.dryRun) {
        return;
    }

    const { device } = await deps.apiClient.getDevice(ctx.deviceId);
    const saved = await saveDeviceOptions(deps.apiClient, ctx.deviceId, device, deps.config.getDeviceOptions());

    if (saved) {
        console.log('Saved device options to the server');
    }
}

export async function startSessionPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    scanErrors: ScanError[],
    onProgress: ProgressHandler
): Promise<void> {
    const startResponse = await deps.apiClient.startSync(ctx.deviceId, {
        dryRun: ctx.options.dryRun,
        direction: ctx.options.direction,
        repositoryPath: ctx.repositoryPath,
        deduplicate: ctx.options.deduplicate,
        scanErrors: scanErrors.map(e => ({ path: e.path, error: e.error })),
        // A dry run doesn't save the device options, so the session previews the local ones
        deviceOptions: ctx.options.dryRun
            ? { namingTemplate: deps.config.getDeviceOptions().namingTemplate }
            : undefined,
    });
    ctx.sessionId = startResponse.sessionId;

    onProgress({ phase: 'server' });
}

/**
 * With deduplication, has the server fingerprint its library before the uploads, one batch per request,
 * so its progress is shown instead of stalling the first upload. Only uploads are deduplicated, so it is
 * skipped in `Down`.
 */
export async function prepareDeduplicatePhase (
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler
): Promise<void> {
    if (!ctx.options.deduplicate || ctx.options.direction === 'Down') {
        return;
    }

    console.log('Fingerprinting server songs for deduplication');
    onProgress({ phase: 'fingerprinting', totalFiles: 0, processedFiles: 0, currentFile: 'Fingerprinting server songs...' });

    let result;
    do {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        result = await deps.apiClient.prepareDeduplicate(ctx.deviceId, ctx.sessionId!);
        onProgress({ phase: 'fingerprinting', totalFiles: result.total, processedFiles: result.processed });
    } while (!result.done);

    console.log(`Fingerprinted ${result.total} server songs for deduplication`);
}

export async function resolveConflictsPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    conflictRecords: SyncRecordItem[],
    updateLocalRecords: SyncRecordItem[],
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<void> {
    const resolveResult = await actionConflict(
        deps.apiClient,
        deps.fileOps,
        deps.userPrompt,
        ctx,
        conflictRecords,
        updateLocalRecords,
        toUpdatePaths,
        (progress) => onProgress({ phase: 'resolving', ...progress })
    );

    ctx.result = addDeltaToResult(ctx.result, resolveResult.counts ?? EMPTY_COUNTS);

    trackConflictedPaths(ctx, conflictRecords, resolveResult.records, toUpdatePaths);

    const clientActions = resolveResult.records.filter(r => r.action === 'UpdateLocal' || r.action === 'Rename');
    ctx.pendingActions = mergePendingActions(ctx.pendingActions ?? [], clientActions);

    const superseded = new Set<SyncRecordItem>([...conflictRecords, ...updateLocalRecords]);
    ctx.pendingActions = ctx.pendingActions.filter(r => !superseded.has(r));
}

export async function uploadPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    files: SyncFileInfo[],
    onProgress: ProgressHandler
): Promise<void> {
    if (ctx.options.direction === 'Down') {
        console.log('Skipping upload phase (direction: down)');
        return;
    }

    if (files.length === 0) {
        return;
    }

    // Resets the progress left by the previous phase (fingerprinting) before the first check returns
    ctx.processedFiles = 0;
    onProgress({ phase: 'upload', totalFiles: files.length, processedFiles: 0, currentFile: '' });

    const chunkSize = deps.config.getChunkSize();
    const chunks = chunkArray(files, chunkSize);
    let checkedFiles = 0;

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
            checkedFiles += chunk.length;
            ctx.processedFiles = checkedFiles;
            onProgress({
                phase: 'upload',
                processedFiles: ctx.processedFiles,
                error: ctx.result.error,
                errorMessage: `Chunk ${i + 1} failed: ${e instanceof Error ? e.message : String(e)}`,
            });
            continue;
        }

        ctx.result = addDeltaToResult(ctx.result, syncResponse.counts ?? EMPTY_COUNTS);

        if (syncResponse.records.length > 0) {
            ctx.pendingActions = mergePendingActions(ctx.pendingActions ?? [], syncResponse.records);
        }

        const conflictRecords = syncResponse.records.filter(r => r.action === 'Conflict');
        const updateLocalRecords = syncResponse.records.filter(r => r.action === 'UpdateLocal');
        const toCreateRecords = syncResponse.records.filter(r => r.action === 'CreateRemote');
        const toUpdateRecords = syncResponse.records.filter(r => r.action === 'UpdateRemote');
        const toUpdatePaths = new Set(toUpdateRecords.map(r => r.filePath));

        // The files the check left nothing to resolve or upload for are done
        const pendingFiles = conflictRecords.length + updateLocalRecords.length + toCreateRecords.length + toUpdateRecords.length;
        ctx.processedFiles = checkedFiles + Math.max(0, chunk.length - pendingFiles);
        reportUploadProgress(ctx, onProgress);

        if (conflictRecords.length > 0 || updateLocalRecords.length > 0) {
            await resolveConflictsPhase(deps, ctx, conflictRecords, updateLocalRecords, toUpdatePaths, onProgress);
        }

        await processChunkUploads(deps, ctx, toCreateRecords, toUpdateRecords, toUpdatePaths, onProgress);

        // Also covers the files that were neither resolved nor uploaded (unreadable, failed request)
        checkedFiles += chunk.length;
        ctx.processedFiles = checkedFiles;
        reportUploadProgress(ctx, onProgress);
    }
}

export async function serverActionsPhase (
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler
): Promise<ActionResult[]> {
    if (ctx.options.direction === 'Up') {
        console.log('Skipping server actions phase (direction: up)');
        return [];
    }

    if (deps.state.isCancelled) {
        throw new SyncCancelledError();
    }

    onProgress({ phase: 'server', currentFile: '' });

    const pendingActionsResponse = await deps.apiClient.createPendingActions(ctx.deviceId, ctx.sessionId!);
    ctx.pendingActions = mergePendingActions(ctx.pendingActions ?? [], pendingActionsResponse.records);

    const pendingActions = ctx.pendingActions;
    const serverResults: ActionResult[] = [];

    // Resets the progress left by the upload phase before the first action finishes
    onProgress({ phase: 'server', totalFiles: pendingActions.length, processedFiles: 0, currentFile: '' });

    for (const record of pendingActions) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        onProgress({ currentFile: formatFilePath(record.filePath, ctx.repositoryPath) });

        if (ctx.uploadedPaths.has(record.filePath) && record.action !== 'CreateLocal' && record.action !== 'UpdateLocal') {
            await deps.apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
                recordIds: [record.id],
            });
            continue;
        }

        if (record.action === 'UpdateLocal' && ctx.conflictedPaths.has(record.filePath)) {
            console.log(`Skipping download for song ${record.songId} at ${record.filePath} - unresolved conflict`);
            const result = await reportFailure(
                deps.apiClient, ctx, record.id, record.filePath, record.songId ?? undefined,
                'Unresolved conflict', record.reason ?? 'Server-initiated update'
            );
            serverResults.push(result);
            if (result.counts) {
                ctx.result = addDeltaToResult(ctx.result, result.counts);
            }
        } else if (record.action === 'CreateLocal' || record.action === 'UpdateLocal') {
            const result = record.action === 'CreateLocal'
                ? await actionCreateLocal(
                    deps.apiClient,
                    deps.fileOps,
                    ctx,
                    record.songId,
                    record.filePath,
                    ctx.decodedRepoPath,
                    record.id,
                    record.reason ?? undefined
                )
                : await actionUpdateLocal(
                    deps.apiClient,
                    deps.fileOps,
                    ctx,
                    record.songId,
                    record.filePath,
                    ctx.decodedRepoPath,
                    record.id,
                    record.reason ?? undefined,
                    record.data?.localSourcePath ?? undefined
                );
            serverResults.push(result);
            if (result.counts) {
                ctx.result = addDeltaToResult(ctx.result, result.counts);
            }
        } else if (record.action === 'DeleteLocal') {
            const result = await actionDeleteLocal(
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
        } else if (record.action === 'Unlink') {
            const result = await actionUnlink(
                deps.apiClient,
                ctx,
                record.filePath,
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
            const previousPath = (record.data as RenameData | null)?.previousPath;
            let result: ActionResult;
            if (!previousPath) {
                console.warn(`Skipping rename of record ${record.id} to ${record.filePath} - missing rename data`);
                result = await reportFailure(
                    deps.apiClient, ctx, record.id, record.filePath, record.songId ?? undefined,
                    'Missing rename data', 'Server-initiated rename'
                );
            } else if (ctx.conflictedPaths.has(previousPath)) {
                console.log(`Skipping rename of ${previousPath} to ${record.filePath} - unresolved conflict`);
                result = await reportFailure(
                    deps.apiClient, ctx, record.id, record.filePath, record.songId ?? undefined,
                    'Unresolved conflict', `Rename from '${previousPath}'`
                );
            } else {
                result = await actionRename(
                    deps.apiClient,
                    deps.fileOps,
                    ctx,
                    record.filePath,
                    previousPath,
                    ctx.decodedRepoPath,
                    record.id
                );
            }
            serverResults.push(result);
            if (result.counts) {
                ctx.result = addDeltaToResult(ctx.result, result.counts);
            }
        }

        onProgress({
            createLocal: ctx.result.createLocal,
            updateLocal: ctx.result.updateLocal,
            deleteLocal: ctx.result.deleteLocal,
            error: ctx.result.error,
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
    ctx.result.deleteLocal = commitResponse.deleteLocalCount;
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
    ctx.result.deleteLocal = completeResponse.deleteLocalCount;
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
    toCreateRecords: SyncRecordItem[],
    toUpdateRecords: SyncRecordItem[],
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<void> {
    for (const createRecord of toCreateRecords) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        const result = await actionCreateRemote(
            deps.apiClient, deps.fileOps, ctx, uploadFileInfo(ctx, createRecord),
            createRecord.reason ?? undefined
        );
        if (result.counts) {
            ctx.result = addDeltaToResult(ctx.result, result.counts);
        }
        queueUploadClientActions(ctx, result);
        ctx.uploadedPaths.add(createRecord.filePath);

        ctx.processedFiles++;
        reportUploadProgress(ctx, onProgress, createRecord.filePath);
    }

    for (const updateRecord of toUpdateRecords) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        if (!toUpdatePaths.has(updateRecord.filePath)) {
            continue;
        }

        const result = await actionUpdateRemote(
            deps.apiClient, deps.fileOps, ctx, uploadFileInfo(ctx, updateRecord),
            updateRecord.reason ?? undefined
        );
        if (result.counts) {
            ctx.result = addDeltaToResult(ctx.result, result.counts);
        }
        queueUploadClientActions(ctx, result);
        ctx.uploadedPaths.add(updateRecord.filePath);

        ctx.processedFiles++;
        reportUploadProgress(ctx, onProgress, updateRecord.filePath);
    }
}

/**
 * Builds the file to upload from a CreateRemote/UpdateRemote record: its path joined to the repository, and the
 * timestamps the server recorded when checking it (as the CLI's SyncCheckCreateUpdateData).
 */
function uploadFileInfo (ctx: SyncContext, record: SyncRecordItem): SyncFileBase {
    const data = record.data as { modifiedAt?: string | null; createdAt?: string | null } | null;
    return {
        relativePath: record.filePath,
        fullPath: `${ctx.decodedRepoPath}/${record.filePath}`,
        modifiedAt: new Date(data?.modifiedAt ?? NaN),
        createdAt: new Date(data?.createdAt ?? NaN),
    };
}

function reportUploadProgress (ctx: SyncContext, onProgress: ProgressHandler, filePath?: string): void {
    onProgress({
        phase: 'upload',
        processedFiles: ctx.processedFiles,
        currentFile: filePath ? formatFilePath(filePath, ctx.repositoryPath) : '',
        createRemote: ctx.result.createRemote,
        updateRemote: ctx.result.updateRemote,
        skipped: ctx.result.skipped,
        error: ctx.result.error,
        conflict: ctx.result.conflict,
    });
}

/**
 * Queues the device actions the server recorded for an uploaded file (an UpdateLocal when the file is a
 * previous version of a song), so the server actions phase performs them in this session.
 */
function queueUploadClientActions (ctx: SyncContext, result: ActionResult): void {
    const clientActions = (result.records ?? []).filter(r => r.action === 'UpdateLocal' || r.action === 'Rename');
    if (clientActions.length > 0) {
        ctx.pendingActions = mergePendingActions(ctx.pendingActions ?? [], clientActions);
    }
}

/**
 * Marks the paths of conflicts, so the server actions phase does not download over (or rename) their local files.
 * Other paths of the same song sync normally. The set only grows during a session: a path is unmarked only when
 * the resolve result settles it (UpdateTimestamp, UpdateLocal or Skipped). If the resolve request fails, its paths
 * stay marked. A conflict whose local file will be uploaded is not marked.
 */
function trackConflictedPaths (
    ctx: SyncContext,
    conflictRecords: SyncRecordItem[],
    resolvedRecords: SyncRecordItem[],
    toUpdatePaths: Set<string>
): void {
    for (const record of [...conflictRecords, ...resolvedRecords]) {
        if ((record.action === 'Conflict' || record.action === 'Error') && !toUpdatePaths.has(record.filePath)) {
            ctx.conflictedPaths.add(record.filePath);
        }
    }

    for (const record of resolvedRecords) {
        if (record.action === 'UpdateTimestamp' || record.action === 'UpdateLocal' || record.action === 'Skipped') {
            ctx.conflictedPaths.delete(record.filePath);
        }
    }
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
