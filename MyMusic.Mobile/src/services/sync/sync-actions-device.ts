import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncFileBase, ActionResult, ResolveConflictsResult, SyncActionCounts, ProgressHandler, SyncRecordItem} from './types';
import type {SyncConflictResolveItem, SyncPotentialUpdateResolveItem, RenameData} from '../../api/types';
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
    conflictRecords: SyncRecordItem[],
    updateLocalRecords: SyncRecordItem[],
    toUpdatePaths: Set<string>,
    onProgress: ProgressHandler
): Promise<ResolveConflictsResult> {
    if (conflictRecords.length === 0 && updateLocalRecords.length === 0) {
        return { records: [], counts: undefined };
    }

    onProgress({ phase: 'resolving', currentFile: 'Checking conflicts...' });

    const resolveItems: SyncConflictResolveItem[] = [];

    for (const conflict of conflictRecords) {
        const relativePath = conflict.filePath;

        try {
            const fullPath = ctx.decodedRepoPath ? `${ctx.decodedRepoPath}/${relativePath}` : relativePath;
            if (!fileOps.fileExists(fullPath)) {
                console.error('Conflict file not found locally:', relativePath);
                continue;
            }

            const fileContentBase64 = await fileOps.readFileBase64(fullPath);
            resolveItems.push({
                path: relativePath,
                songId: conflict.songId,
                fileContentBase64,
                localModifiedAt: safeToIsoString((conflict.data as any)?.localModifiedAt ? new Date((conflict.data as any).localModifiedAt) : new Date())!,
            });
        } catch (e) {
            console.error('Failed to read file for conflict resolution:', relativePath, e);
        }
    }

    const potentialUpdateItems: SyncPotentialUpdateResolveItem[] = [];

    for (const update of updateLocalRecords) {
        const relativePath = update.filePath;

        try {
            const fullPath = ctx.decodedRepoPath ? `${ctx.decodedRepoPath}/${relativePath}` : relativePath;
            if (!fileOps.fileExists(fullPath)) {
                console.error('Potential update file not found locally:', relativePath);
                continue;
            }

            const fileContentBase64 = await fileOps.readFileBase64(fullPath);
            const updateData = update.data as any;
            potentialUpdateItems.push({
                path: relativePath,
                songId: update.songId!,
                fileContentBase64,
                localModifiedAt: safeToIsoString(updateData?.localModifiedAt ? new Date(updateData.localModifiedAt) : new Date())!,
                lastSyncedAt: safeToIsoString(updateData?.lastSyncedAt ? new Date(updateData.lastSyncedAt) : new Date())!,
            });
        } catch (e) {
            console.error('Failed to read file for potential update resolution:', relativePath, e);
        }
    }

    if (resolveItems.length === 0 && potentialUpdateItems.length === 0) {
        return { records: [], counts: undefined };
    }

    try {
        const resolveResponse = await apiClient.resolveConflicts(ctx.deviceId, ctx.sessionId!, {
            conflicts: resolveItems,
            potentialUpdates: potentialUpdateItems,
        });

        ctx.result = addDeltaToResult(ctx.result, resolveResponse.counts ?? EMPTY_COUNTS);

        for (const record of resolveResponse.records) {
            switch (record.action) {
                case 'UpdateTimestamp':
                    console.log('Resolved conflict for', record.filePath, ':', record.reason);
                    break;
                case 'Conflict':
                    if (ctx.options.treatConflictsAsErrors) {
                        console.error('Conflict (as error):', record.filePath, record.reason);
                        ctx.result.error++;
                        ctx.result.conflict++;
                    } else {
                        const resolution = await userPrompt.promptConflictResolution(record.filePath);
                        if (resolution === 'upload') {
                            toUpdatePaths.add(record.filePath);
                        } else {
                            ctx.result.error++;
                        }
                    }
                    break;
                case 'CreateRemote':
                case 'UpdateRemote':
                    toUpdatePaths.add(record.filePath);
                    break;
                case 'UpdateLocal':
                case 'Rename':
                case 'Error':
                    console.log(`${record.action} action for ${record.filePath}: ${record.reason}`);
                    break;
            }
        }

        return { records: resolveResponse.records, counts: resolveResponse.counts };
    } catch (e) {
        console.error('Failed to resolve conflicts:', e);
        return { records: [], counts: undefined };
    }
}