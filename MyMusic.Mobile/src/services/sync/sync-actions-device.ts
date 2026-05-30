import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncFileBase, ActionResult, ResolveConflictsResult, SyncActionCounts, ProgressHandler, SyncRecordItem} from './types';
import type {SyncPotentialConflictItem, SyncPotentialUpdateItem, SyncConflictResolveItem, SyncPotentialUpdateResolveItem} from '../../api/types';
import {addDeltaToResult} from './types';
import {safeToIsoString} from './utils';

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

export async function actionCreateRemote(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    file: SyncFileBase,
    reason?: string
): Promise<ActionResult> {
    if (!fileOps.fileExists(file.fullPath)) {
        return {
            action: 'Error',
            filePath: file.relativePath,
            source: 'Device',
            reason,
            errorMessage: `File not found: ${file.fullPath}`,
        };
    }

    try {
        const filename = file.relativePath.split('/').pop();
        if (!filename) {
            throw new Error(`Invalid file path: relativePath is empty for ${file.fullPath}`);
        }

        const uploadResult = await apiClient.uploadFile(
            ctx.deviceId,
            ctx.sessionId!,
            {
                uri: file.fullPath,
                name: filename,
            },
            file.relativePath,
            safeToIsoString(file.modifiedAt)!,
            safeToIsoString(file.createdAt)!
        );

        return {
            action: 'CreateRemote',
            filePath: file.relativePath,
            source: 'Device',
            reason,
            songId: uploadResult.songId ?? undefined,
            counts: uploadResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            action: 'Error',
            filePath: file.relativePath,
            source: 'Device',
            reason,
            errorMessage,
        };
    }
}

export async function actionUpdateRemote(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    file: SyncFileBase,
    reason?: string
): Promise<ActionResult> {
    if (!fileOps.fileExists(file.fullPath)) {
        return {
            action: 'Error',
            filePath: file.relativePath,
            source: 'Device',
            reason,
            errorMessage: `File not found: ${file.fullPath}`,
        };
    }

    try {
        const filename = file.relativePath.split('/').pop();
        if (!filename) {
            throw new Error(`Invalid file path: relativePath is empty for ${file.fullPath}`);
        }

        const uploadResult = await apiClient.uploadFile(
            ctx.deviceId,
            ctx.sessionId!,
            {
                uri: file.fullPath,
                name: filename,
            },
            file.relativePath,
            safeToIsoString(file.modifiedAt)!,
            safeToIsoString(file.createdAt)!
        );

        return {
            action: 'UpdateRemote',
            filePath: file.relativePath,
            source: 'Device',
            reason,
            songId: uploadResult.songId ?? undefined,
            counts: uploadResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            action: 'Error',
            filePath: file.relativePath,
            source: 'Device',
            reason,
            errorMessage,
        };
    }
}

export async function actionCreateLocal(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    userPrompt: IUserPrompt,
    ctx: SyncContext,
    songId: number | null,
    path: string,
    decodedRepoPath: string,
    recordId: number,
    reason?: string
): Promise<ActionResult | null> {
    const fullPath = `${decodedRepoPath}/${path}`;
    const fileExists = fileOps.fileExists(fullPath);

    if (!ctx.options.dryRun && fileExists && !ctx.options.autoConfirm) {
        const confirmed = await userPrompt.confirmDeletion(path);
        if (!confirmed) {
            return null;
        }
    }

    const baseReason = reason ?? 'Server-initiated download';

    if (ctx.options.dryRun) {
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
            recordIds: [recordId],
        });
        return {
            action: 'CreateLocal',
            filePath: path,
            source: 'Server',
            reason: baseReason,
            songId: songId ?? undefined,
            recordId,
            counts: ackResult.counts,
        };
    }

    try {
        await fileOps.ensureDirectory(fullPath);

        const tempPath = `${fullPath}.tmp`;
        try {
            const blob = await apiClient.downloadSong(songId!);
            await fileOps.writeFile(tempPath, blob);

            if (fileExists) {
                await fileOps.deleteFile(fullPath);
            }

            await fileOps.moveFile(tempPath, fullPath);
        } finally {
            if (fileOps.fileExists(tempPath)) {
                await fileOps.deleteFile(tempPath);
            }
        }

        const modifiedAt = fileOps.getModificationTime(fullPath);
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
            recordIds: [recordId],
            modifiedAt: modifiedAt ? safeToIsoString(modifiedAt) : undefined,
        });

        return {
            action: 'CreateLocal',
            filePath: path,
            source: 'Server',
            reason: baseReason,
            songId: songId ?? undefined,
            recordId,
            counts: ackResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            action: 'Error',
            filePath: path,
            source: 'Server',
            reason: `${baseReason} failed`,
            errorMessage,
            songId: songId ?? undefined,
        };
    }
}

export async function actionUpdateLocal(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    userPrompt: IUserPrompt,
    ctx: SyncContext,
    songId: number | null,
    path: string,
    decodedRepoPath: string,
    recordId: number,
    reason?: string
): Promise<ActionResult | null> {
    return actionCreateLocal(apiClient, fileOps, userPrompt, ctx, songId, path, decodedRepoPath, recordId, reason);
}

export async function actionDelete(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    userPrompt: IUserPrompt,
    ctx: SyncContext,
    path: string,
    decodedRepoPath: string,
    songId?: number,
    recordId?: number,
    reason?: string
): Promise<ActionResult | null> {
    const fullPath = `${decodedRepoPath}/${path}`;
    const fileExists = fileOps.fileExists(fullPath);

    if (!fileExists) {
        if (recordId) {
            const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId] });
            ctx.result = addDeltaToResult(ctx.result, ackResult.counts ?? EMPTY_COUNTS);
        }
        return null;
    }

    let shouldDelete = true;

    if (!ctx.options.autoConfirm && !ctx.options.dryRun) {
        shouldDelete = await userPrompt.confirmDeletion(path);
    }

    if (!shouldDelete) {
        return null;
    }

    const baseReason = reason ?? 'Server-initiated removal';

    if (ctx.options.dryRun) {
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId ?? 0] });
        return {
            action: 'Delete',
            filePath: path,
            source: 'Server',
            reason: baseReason,
            songId,
            recordId,
            counts: ackResult.counts,
        };
    }

    try {
        await fileOps.deleteFile(fullPath);
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId ?? 0] });

        return {
            action: 'Delete',
            filePath: path,
            source: 'Server',
            reason: baseReason,
            songId,
            recordId,
            counts: ackResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            action: 'Error',
            filePath: path,
            source: 'Server',
            reason: `${baseReason} failed`,
            errorMessage,
            songId,
        };
    }
}

export async function actionRename(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    relativePath: string,
    previousRelativePath: string,
    decodedRepoPath: string,
    recordId: number
): Promise<ActionResult> {
    const fullPath = `${decodedRepoPath}/${relativePath}`;
    const previousFullPath = `${decodedRepoPath}/${previousRelativePath}`;

    if (ctx.options.dryRun) {
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
            recordIds: [recordId],
        });
        return {
            action: 'Rename',
            filePath: relativePath,
            source: 'Server',
            reason: `Renamed from '${previousRelativePath}'`,
            recordId,
            counts: ackResult.counts,
        };
    }

    try {
        if (fileOps.fileExists(previousFullPath)) {
            await fileOps.ensureDirectory(fullPath);
            await fileOps.moveFile(previousFullPath, fullPath);
            await fileOps.deleteEmptyDirectories(previousFullPath, decodedRepoPath);
        }

        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
            recordIds: [recordId],
        });

        return {
            action: 'Rename',
            filePath: relativePath,
            source: 'Server',
            reason: `Renamed from '${previousRelativePath}'`,
            recordId,
            counts: ackResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            action: 'Error',
            filePath: relativePath,
            source: 'Server',
            reason: `Rename from '${previousRelativePath}' failed`,
            errorMessage,
        };
    }
}

export async function actionConflict(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    userPrompt: IUserPrompt,
    ctx: SyncContext,
    potentialConflicts: SyncPotentialConflictItem[],
    potentialUpdates: SyncPotentialUpdateItem[],
    chunk: Array<{ relativePath: string; fullPath: string }>,
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<ResolveConflictsResult> {
    if (potentialConflicts.length === 0 && potentialUpdates.length === 0) {
        return { conflicts: 0, toUpdatePaths: new Set(), updateLocalRecords: [] };
    }

    if (ctx.options.dryRun) {
        ctx.result.conflict += potentialConflicts.length;
        onProgress({
            phase: 'resolving',
            currentFile: `${potentialConflicts.length} conflicts detected (dry-run)`,
            conflict: ctx.result.conflict,
        });
        return { conflicts: potentialConflicts.length, toUpdatePaths: new Set(), updateLocalRecords: [] };
    }

    onProgress({ phase: 'resolving', currentFile: 'Checking conflicts...' });

    const resolveItems: SyncConflictResolveItem[] = [];

    for (const conflict of potentialConflicts) {
        const file = chunk.find(f => f.relativePath === conflict.path);
        if (!file) {
            continue;
        }

        try {
            const fileContentBase64 = await fileOps.readFileBase64(file.fullPath);
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

    const potentialUpdateItems: SyncPotentialUpdateResolveItem[] = [];

    for (const update of potentialUpdates) {
        const file = chunk.find(f => f.relativePath === update.path);
        if (!file) {
            continue;
        }

        try {
            const fileContentBase64 = await fileOps.readFileBase64(file.fullPath);
            potentialUpdateItems.push({
                path: update.path,
                songId: update.songId,
                fileContentBase64,
                localModifiedAt: safeToIsoString(update.localModifiedAt)!,
                lastSyncedAt: safeToIsoString(update.lastSyncedAt)!,
            });
        } catch (e) {
            console.error('Failed to read file for potential update resolution:', update.path, e);
        }
    }

    if (resolveItems.length === 0 && potentialUpdateItems.length === 0) {
        return { conflicts: 0, toUpdatePaths: new Set(), updateLocalRecords: [] };
    }

    try {
        const resolveResponse = await apiClient.resolveConflicts(ctx.deviceId, ctx.sessionId!, {
            conflicts: resolveItems,
            potentialUpdates: potentialUpdateItems,
        });

        ctx.result = addDeltaToResult(ctx.result, resolveResponse.counts ?? EMPTY_COUNTS);

        for (const resolved of resolveResponse.resolved) {
            console.log('Resolved conflict for', resolved.path, ':', resolved.reason);
        }

        for (const toUploadItem of resolveResponse.toUpload) {
            toUpdatePaths.add(toUploadItem.path);
        }

        for (const conflict of resolveResponse.conflicts) {
            if (ctx.options.treatConflictsAsErrors) {
                console.error('Conflict (as error):', conflict.path, conflict.reason);
                ctx.result.error++;
                ctx.result.conflict++;
            } else {
                const resolution = await userPrompt.promptConflictResolution(conflict.path);

                if (resolution === 'upload') {
                    toUpdatePaths.add(conflict.path);
                } else {
                    ctx.result.error++;
                }
            }
        }

        return { conflicts: resolveResponse.conflicts.length, toUpdatePaths, updateLocalRecords: resolveResponse.updateLocalRecords };
    } catch (e) {
        console.error('Failed to resolve conflicts:', e);
        return { conflicts: potentialConflicts.length, toUpdatePaths: new Set(), updateLocalRecords: [] };
    }
}