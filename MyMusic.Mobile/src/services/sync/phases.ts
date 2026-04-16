// @TODO: CLI has SyncDirection (Both/Up/Down) for controlling upload/download phases.
// Mobile always syncs both ways. Consider adding this feature for fine-grained sync control.
import type {SyncDeps, SyncContext, RecordItem, ConflictResolution, SyncFileInfo, ScanError, SyncConflict, ProgressHandler, PendingActionItem} from './types';
import type {SyncConflictResolveItem, SyncPotentialConflictItem} from '../../api/types';
import {SyncCancelledError} from '../syncService';
import {safeToIsoString, chunkArray, formatFilePath} from './utils';
import {uploadOneFile, downloadOneFile, removeOneFile} from './atomic-operations';

export interface ScanPhaseResult {
    files: SyncFileInfo[];
    errors: ScanError[];
    estimatedTotal: number;
}

export async function scanPhase(
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

    const {files, errors} = await deps.scanner(ctx.repositoryPath, {
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
            scanErrors.push({path, error});
        },
    });

    ctx.result.failed += errors.length;

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

    return {files, errors: scanErrors, estimatedTotal: currentEstimate};
}

export async function startSessionPhase(
    deps: SyncDeps,
    ctx: SyncContext,
    scanErrors: ScanError[],
    onProgress: ProgressHandler
): Promise<void> {
    const startResponse = await deps.apiClient.startSync(ctx.deviceId, {
        dryRun: ctx.options.dryRun,
        repositoryPath: ctx.repositoryPath,
    });
    const sessionId = startResponse.sessionId;
    ctx.sessionId = sessionId;

    if (scanErrors.length > 0) {
        await deps.apiClient.recordChunk(ctx.deviceId, sessionId, {
            records: scanErrors.map(e => ({
                filePath: e.path,
                action: 'Error',
                source: 'Device',
                errorMessage: e.error,
            })),
        });
    }

    onProgress({phase: 'server'});
}

export async function resolveConflictsPhase(
    deps: SyncDeps,
    ctx: SyncContext,
    potentialConflicts: SyncPotentialConflictItem[],
    chunk: SyncFileInfo[],
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<void> {
    if (potentialConflicts.length === 0) {
        return;
    }

    if (ctx.options.dryRun) {
        console.log(
            `Dry-run: ${potentialConflicts.length} potential conflicts detected (not resolved)`
        );
        ctx.result.conflicts += potentialConflicts.length;

        onProgress({
            phase: 'resolving',
            currentFile: `${potentialConflicts.length} conflicts detected (dry-run)`,
            conflicts: ctx.result.conflicts,
        });
        return;
    }

    onProgress({phase: 'resolving', currentFile: 'Checking conflicts...'});

    const resolveItems: SyncConflictResolveItem[] = [];

    for (const conflict of potentialConflicts) {
        const file = chunk.find(f => f.relativePath === conflict.path);
        if (!file) {
            continue;
        }

        try {
            const fileContentBase64 = await deps.fileOps.readFileBase64(file.fullPath);
            resolveItems.push({
                path: conflict.path,
                songId: conflict.songId,
                fileContentBase64,
                localModifiedAt: safeToIsoString(conflict.localModifiedAt)!,
            });
        } catch (e) {
            console.error('Failed to read file for conflict resolution:', conflict.path, e);
        }
    }

    if (resolveItems.length === 0) {
        return;
    }

    try {
        const resolveResponse = await deps.apiClient.resolveConflicts(ctx.deviceId, {
            conflicts: resolveItems,
        });

        for (const resolved of resolveResponse.resolved) {
            console.log('Resolved conflict for', resolved.path, ':', resolved.reason);
        }

        for (const toUploadItem of resolveResponse.toUpload) {
            toUpdatePaths.add(toUploadItem.path);
        }

        for (const conflict of resolveResponse.conflicts) {
            ctx.result.conflicts++;

            if (ctx.options.treatConflictsAsErrors) {
                ctx.result.failed++;
                console.error('Conflict (as error):', conflict.path, conflict.reason);
            } else {
                const resolution = await deps.userPrompt.promptConflictResolution(conflict.path);

                if (resolution === 'upload') {
                    toUpdatePaths.add(conflict.path);
                } else if (resolution === 'download') {
                    await handleDownloadConflict(deps, ctx, conflict, potentialConflicts);
                    ctx.result.downloaded++;
                } else {
                    ctx.result.failed++;
                }
            }
        }
    } catch (e) {
        console.error('Failed to resolve conflicts:', e);
    }
}

async function handleDownloadConflict(
    deps: SyncDeps,
    ctx: SyncContext,
    conflict: SyncConflict,
    potentialConflicts: SyncPotentialConflictItem[]
): Promise<void> {
    const conflictInfo = potentialConflicts.find(c => c.path === conflict.path);
    if (!conflictInfo || !ctx.sessionId) {
        return;
    }

    const record = await downloadOneFile(
        deps.apiClient,
        deps.fileOps,
        ctx,
        conflictInfo.songId,
        conflictInfo.path,
        ctx.decodedRepoPath
    );

    if (record) {
        await deps.apiClient.recordChunk(ctx.deviceId, ctx.sessionId, {
            records: [record],
        });
    }
}

export async function uploadPhase(
    deps: SyncDeps,
    ctx: SyncContext,
    files: SyncFileInfo[],
    onProgress: ProgressHandler
): Promise<void> {
    const pendingActionsResponse = await deps.apiClient.getPendingActions(ctx.deviceId);
    ctx.pendingActions = pendingActionsResponse.actions;
    ctx.pendingDownloadPaths = extractPendingDownloadPaths(ctx.pendingActions);

    const chunkSize = deps.config.getChunkSize();
    const chunks = chunkArray(files, chunkSize);

    for (let i = 0; i < chunks.length; i++) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }
        const chunk = chunks[i];
        const recordItems: RecordItem[] = [];

        const syncFiles = chunk.map(f => ({
            path: f.relativePath,
            modifiedAt: safeToIsoString(f.modifiedAt)!,
            createdAt: safeToIsoString(f.createdAt)!,
        }));

        const syncResponse = await deps.apiClient.checkSync(ctx.deviceId, {
            files: syncFiles,
            force: ctx.options.force,
        });

        if (syncResponse.pendingActions.length > 0) {
            ctx.pendingActions = mergePendingActions(ctx.pendingActions, syncResponse.pendingActions);
            ctx.pendingDownloadPaths = extractPendingDownloadPaths(ctx.pendingActions);
        }

        const toCreatePaths = new Set(syncResponse.toCreate.map(f => f.path));
        const toUpdatePaths = new Set(syncResponse.toUpdate.map(f => f.path));

        await resolveConflictsPhase(
            deps,
            ctx,
            syncResponse.potentialConflicts,
            chunk,
            toUpdatePaths,
            onProgress
        );

        const uploadRecords = await processChunkUploads(deps, ctx, chunk, syncResponse.toCreate, syncResponse.toUpdate, toUpdatePaths, onProgress);
        recordItems.push(...uploadRecords);

        const skippedRecords = processSkippedFiles(chunk, toCreatePaths, toUpdatePaths, ctx.pendingDownloadPaths);
        recordItems.push(...skippedRecords);
        ctx.result.skipped += skippedRecords.length;

        await deps.apiClient.recordChunk(ctx.deviceId, ctx.sessionId!, {records: recordItems});
    }

    for (let i = 0; i < chunks.length; i++) {
        const chunk = chunks[i];
        for (const file of chunk) {
            ctx.uploadedPaths.add(file.relativePath);
        }
    }
}

export async function serverActionsPhase(
    deps: SyncDeps,
    ctx: SyncContext,
    onProgress: ProgressHandler
): Promise<RecordItem[]> {
    if (deps.state.isCancelled) {
        throw new SyncCancelledError();
    }

    const pendingActions = ctx.pendingActions ?? [];
    const downloadRecordItems: RecordItem[] = [];

    for (const action of pendingActions) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        if (action.action === 'Download') {
            const record = await downloadOneFile(
                deps.apiClient,
                deps.fileOps,
                ctx,
                action.songId!,
                action.path,
                ctx.decodedRepoPath
            );
            if (record) {
                downloadRecordItems.push(record);
            }
        } else if (action.action === 'Remove') {
            const record = await removeOneFile(
                deps.apiClient,
                deps.fileOps,
                deps.userPrompt,
                ctx,
                action.path,
                ctx.decodedRepoPath
            );
            if (record) {
                downloadRecordItems.push({
                    ...record,
                    songId: action.songId ?? undefined,
                });
            }
        }

        onProgress({
            downloaded: ctx.result.downloaded,
            removed: ctx.result.removed,
            processedFiles: pendingActions.indexOf(action) + 1,
            totalFiles: pendingActions.length,
        });
    }

    if (downloadRecordItems.length > 0 && ctx.sessionId) {
        await deps.apiClient.recordChunk(ctx.deviceId, ctx.sessionId, {
            records: downloadRecordItems,
        });
    }

    return downloadRecordItems;
}

export async function completePhase(
    deps: SyncDeps,
    ctx: SyncContext,
    filesCount: number,
    onProgress: ProgressHandler
): Promise<void> {
    onProgress({phase: 'completing'});

    const completeResponse = await deps.apiClient.completeSync(ctx.deviceId, ctx.sessionId!);

    ctx.result.created = completeResponse.createdCount;
    ctx.result.updated = completeResponse.updatedCount;
    ctx.result.skipped = completeResponse.skippedCount;
    ctx.result.downloaded = completeResponse.downloadedCount;
    ctx.result.removed = completeResponse.removedCount;
    ctx.result.failed = completeResponse.errorCount;
    ctx.result.sessionId = ctx.sessionId;

    await deps.config.setLastSyncAt(new Date().toISOString());
    await deps.config.setLastScanTotal(filesCount);
}

async function processChunkUploads(
    deps: SyncDeps,
    ctx: SyncContext,
    chunk: SyncFileInfo[],
    toCreate: Array<{path: string; modifiedAt: Date; createdAt: Date; reason?: string}>,
    toUpdate: Array<{path: string; modifiedAt: Date; createdAt: Date; reason?: string}>,
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<RecordItem[]> {
    const recordItems: RecordItem[] = [];

    for (const fileToCreate of toCreate) {
        if (deps.state.isCancelled) {
            throw new SyncCancelledError();
        }

        const file = chunk.find(f => f.relativePath === fileToCreate.path);
        if (file) {
            const record = await uploadOneFile(deps.apiClient, ctx, file, 'Created', fileToCreate.reason);
            recordItems.push(record);
        }

        onProgress({
            processedFiles: ctx.result.created + ctx.result.updated + ctx.result.skipped + ctx.result.failed + ctx.result.conflicts,
            currentFile: formatFilePath(fileToCreate.path, ctx.repositoryPath),
            created: ctx.result.created,
            updated: ctx.result.updated,
            skipped: ctx.result.skipped,
            failed: ctx.result.failed,
            conflicts: ctx.result.conflicts,
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
            const record = await uploadOneFile(deps.apiClient, ctx, file, 'Updated', fileToUpdate.reason);
            recordItems.push(record);
        }

        onProgress({
            processedFiles: ctx.result.created + ctx.result.updated + ctx.result.skipped + ctx.result.failed + ctx.result.conflicts,
            currentFile: formatFilePath(fileToUpdate.path, ctx.repositoryPath),
            created: ctx.result.created,
            updated: ctx.result.updated,
            skipped: ctx.result.skipped,
            failed: ctx.result.failed,
            conflicts: ctx.result.conflicts,
        });
    }

    return recordItems;
}

function processSkippedFiles(
    chunk: SyncFileInfo[],
    toCreatePaths: Set<string>,
    toUpdatePaths: Set<string>,
    pendingDownloadPaths: Set<string>
): RecordItem[] {
    const skippedRecords: RecordItem[] = [];

    for (const file of chunk) {
        const inCreate = toCreatePaths.has(file.relativePath);
        const inUpdate = toUpdatePaths.has(file.relativePath);
        const isPendingDownload = pendingDownloadPaths.has(file.relativePath);

        if (!inCreate && !inUpdate && !isPendingDownload) {
            skippedRecords.push({
                filePath: file.relativePath,
                action: 'Skipped',
                source: 'Device',
                reason: 'Unchanged',
            });
        }
    }

    return skippedRecords;
}

function extractPendingDownloadPaths(actions: PendingActionItem[]): Set<string> {
    const paths = new Set<string>();
    for (const action of actions) {
        if (action.action === 'Download') {
            paths.add(action.path);
        }
    }
    return paths;
}

function mergePendingActions(
    existing: PendingActionItem[],
    incoming: PendingActionItem[]
): PendingActionItem[] {
    const merged = new Map<string, PendingActionItem>();
    for (const action of existing) {
        merged.set(action.path, action);
    }
    for (const action of incoming) {
        merged.set(action.path, action);
    }
    return Array.from(merged.values());
}