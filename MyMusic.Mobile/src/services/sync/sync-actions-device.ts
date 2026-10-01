import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncFileBase, ActionResult, ResolveConflictsResult, SyncActionCounts, ProgressHandler, SyncRecordItem} from './types';
import type {SyncConflictResolveItem, SyncPotentialUpdateResolveItem, RenameData, ConflictData, SongModifiedAtData} from '../../api/types';
import {safeToIsoString} from './utils';

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
            records: uploadResult.records,
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
            records: uploadResult.records,
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
    ctx: SyncContext,
    songId: number | null,
    path: string,
    decodedRepoPath: string,
    recordId: number,
    reason?: string
): Promise<ActionResult> {
    const fullPath = `${decodedRepoPath}/${path}`;

    if (fileOps.fileExists(fullPath)) {
        console.error('File already exists during create:', path);
        return reportFailure(apiClient, ctx, recordId, path, songId ?? undefined, 'File already exists', 'Unexpected local file during create');
    }

    return downloadAndAck(apiClient, fileOps, ctx, songId, path, decodedRepoPath, recordId, reason, false);
}

export async function actionUpdateLocal(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    songId: number | null,
    path: string,
    decodedRepoPath: string,
    recordId: number,
    reason?: string,
    localSourcePath?: string
): Promise<ActionResult> {
    const fullPath = `${decodedRepoPath}/${path}`;

    if (!fileOps.fileExists(fullPath)) {
        console.error('File not found during update:', path);
        return reportFailure(apiClient, ctx, recordId, path, songId ?? undefined, 'File not found', 'Missing local file during update');
    }

    if (localSourcePath && !fileOps.fileExists(`${decodedRepoPath}/${localSourcePath}`)) {
        console.error(`Source file ${localSourcePath} not found during update of ${path}`);
        return reportFailure(apiClient, ctx, recordId, path, songId ?? undefined, `Source file not found: ${localSourcePath}`, 'Missing local source file during update');
    }

    return downloadAndAck(apiClient, fileOps, ctx, songId, path, decodedRepoPath, recordId, reason, true, localSourcePath);
}

async function downloadAndAck(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    songId: number | null,
    path: string,
    decodedRepoPath: string,
    recordId: number,
    reason: string | undefined,
    isUpdate: boolean,
    localSourcePath?: string
): Promise<ActionResult> {
    const action = isUpdate ? 'UpdateLocal' : 'CreateLocal';
    const baseReason = reason ?? (isUpdate ? 'Server-initiated update' : 'Server-initiated download');
    const fullPath = `${decodedRepoPath}/${path}`;
    const tempPath = `${fullPath}.tmp`;

    try {
        // Dry-run skips the download/move, so there is no real file modification time to report.
        let modifiedAt: Date | null = null;
        if (!ctx.options.dryRun) {
            await fileOps.ensureDirectory(fullPath);

            // A soundalike of a file uploaded in this session gets that file's content, which is only
            // on the device until the commit creates its song
            if (localSourcePath) {
                await fileOps.copyFile(`${decodedRepoPath}/${localSourcePath}`, tempPath);
            } else {
                const blob = await apiClient.downloadSong(songId!);
                await fileOps.writeFile(tempPath, blob);
            }

            if (isUpdate) {
                await fileOps.deleteFile(fullPath);
            }

            await fileOps.moveFile(tempPath, fullPath);

            modifiedAt = fileOps.getModificationTime(fullPath);
        }

        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, {
            recordIds: [recordId],
            modifiedAt: modifiedAt ? safeToIsoString(modifiedAt) : undefined,
        });

        return {
            action,
            filePath: path,
            source: 'Server',
            reason: baseReason,
            songId: songId ?? undefined,
            recordId,
            counts: ackResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return reportFailure(apiClient, ctx, recordId, path, songId ?? undefined, errorMessage, `${baseReason} failed`);
    } finally {
        if (fileOps.fileExists(tempPath)) {
            await fileOps.deleteFile(tempPath);
        }
    }
}

export async function actionDeleteLocal(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    userPrompt: IUserPrompt,
    ctx: SyncContext,
    path: string,
    decodedRepoPath: string,
    songId: number | undefined,
    recordId: number,
    reason?: string
): Promise<ActionResult | null> {
    const fullPath = `${decodedRepoPath}/${path}`;
    const fileExists = fileOps.fileExists(fullPath);

    if (!fileExists) {
        await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId] });
        return null;
    }

    const baseReason = reason ?? 'Server-initiated removal';

    if (!ctx.options.autoConfirm && !ctx.options.dryRun) {
        const confirmed = await userPrompt.confirmDeletion(path);
        if (!confirmed) {
            console.log('Deletion declined by user:', path);
            return reportFailure(apiClient, ctx, recordId, path, songId, 'Deletion declined by user', baseReason);
        }
    }

    if (ctx.options.dryRun) {
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId] });
        return {
            action: 'DeleteLocal',
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
        const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId] });

        return {
            action: 'DeleteLocal',
            filePath: path,
            source: 'Server',
            reason: baseReason,
            songId,
            recordId,
            counts: ackResult.counts,
        };
    } catch (e) {
        const errorMessage = e instanceof Error ? e.message : String(e);
        return reportFailure(apiClient, ctx, recordId, path, songId, errorMessage, `${baseReason} failed`);
    }
}

/**
 * Acknowledges an Unlink record without touching the local filesystem.
 * Unlink is emitted by orphan detection when the client's local file is
 * already gone; the server only needs to sever the SongDevice association.
 */
export async function actionUnlink(
    apiClient: ISyncApiClient,
    ctx: SyncContext,
    path: string,
    songId: number | undefined,
    recordId: number,
    reason?: string
): Promise<ActionResult | null> {
    const baseReason = reason ?? 'Orphaned: path not present locally';

    const ackResult = await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId] });

    return {
        action: 'Unlink',
        filePath: path,
        source: 'Server',
        reason: baseReason,
        songId,
        recordId,
        counts: ackResult.counts,
    };
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
        return reportFailure(apiClient, ctx, recordId, relativePath, undefined, errorMessage, `Rename from '${previousRelativePath}' failed`);
    }
}

/**
 * Reports a client action that could not be performed as an `Error` linked to its record.
 * The server acknowledges the record, so the commit is not blocked, and does not apply it, so
 * the server state keeps reflecting what is actually on the device.
 */
export async function reportFailure(
    apiClient: ISyncApiClient,
    ctx: SyncContext,
    recordId: number,
    filePath: string,
    songId: number | undefined,
    errorMessage: string,
    reason: string
): Promise<ActionResult> {
    let counts: SyncActionCounts | undefined;
    try {
        const response = await apiClient.reportSyncError(ctx.deviceId, ctx.sessionId!, {
            filePath,
            errorMessage,
            songId,
            recordId,
        });
        counts = response.counts;
    } catch (e) {
        console.error(`Failed to report the error of record ${recordId}: ${filePath}`, e);
    }

    return {
        action: 'Error',
        filePath,
        source: 'Server',
        reason,
        errorMessage,
        songId,
        recordId,
        counts,
    };
}

/**
 * Maximum base64 payload size (in characters, roughly bytes) sent per resolve-conflicts request.
 * The server has a 100 MB request body limit; chunking keeps each request well under that ceiling
 * while leaving headroom for the JSON envelope and metadata. Same limit as the CLI.
 */
export const MAX_RESOLVE_REQUEST_SIZE = 20_000_000;

/** The kind of item sent for conflict resolution, used in log messages. */
const ResolveItemKind = {
    Conflict: 'Conflict',
    PotentialUpdate: 'Potential update',
} as const;

type ResolveItemKind = typeof ResolveItemKind[keyof typeof ResolveItemKind];

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

        if (conflict.songId == null) {
            console.warn('Skipping conflict with no songId:', relativePath);
            continue;
        }

        const fileContentBase64 = await readLocalFileBase64(ResolveItemKind.Conflict, fileOps, ctx, relativePath);
        if (fileContentBase64 === null) {
            continue;
        }

        const conflictData = conflict.data as ConflictData | null | undefined;
        resolveItems.push({
            path: relativePath,
            songId: conflict.songId,
            fileContentBase64,
            localModifiedAt: dataDateOrNow(conflictData?.localModifiedAt),
        });
    }

    const potentialUpdateItems: SyncPotentialUpdateResolveItem[] = [];

    for (const update of updateLocalRecords) {
        const relativePath = update.filePath;

        if (update.songId == null) {
            console.warn('Skipping potential update with no songId:', relativePath);
            continue;
        }

        const fileContentBase64 = await readLocalFileBase64(ResolveItemKind.PotentialUpdate, fileOps, ctx, relativePath);
        if (fileContentBase64 === null) {
            continue;
        }

        const updateData = update.data as SongModifiedAtData | null | undefined;
        potentialUpdateItems.push({
            path: relativePath,
            songId: update.songId,
            fileContentBase64,
            localModifiedAt: dataDateOrNow(updateData?.localModifiedAt),
            lastSyncedAt: dataDateOrNow(updateData?.lastSyncedAt),
        });
    }

    if (resolveItems.length === 0 && potentialUpdateItems.length === 0) {
        return { records: [], counts: undefined };
    }

    const chunks = buildResolveChunks(resolveItems, potentialUpdateItems);
    console.log(`Resolving ${resolveItems.length} conflicts and ${potentialUpdateItems.length} potential updates in ${chunks.length} chunk(s)`);

    const allRecords: SyncRecordItem[] = [];
    let aggregatedCounts: SyncActionCounts | undefined;

    try {
        for (const chunk of chunks) {
            const resolveResponse = await apiClient.resolveConflicts(ctx.deviceId, ctx.sessionId!, chunk);

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

            allRecords.push(...resolveResponse.records);
            aggregatedCounts = addCounts(aggregatedCounts, resolveResponse.counts);
        }
    } catch (e) {
        console.error('Failed to resolve conflicts:', e);
    }

    return { records: allRecords, counts: aggregatedCounts };
}

/**
 * Reads a local file for conflict resolution. Returns null, after logging, when the file is missing
 * or cannot be read, so the other items are still resolved.
 */
async function readLocalFileBase64(kind: ResolveItemKind, fileOps: IFileOps, ctx: SyncContext, relativePath: string): Promise<string | null> {
    try {
        const fullPath = ctx.decodedRepoPath ? `${ctx.decodedRepoPath}/${relativePath}` : relativePath;
        if (!fileOps.fileExists(fullPath)) {
            console.error(`${kind} file not found locally:`, relativePath);
            return null;
        }

        return await fileOps.readFileBase64(fullPath);
    } catch (e) {
        console.error(`Failed to read file for ${kind} resolution:`, relativePath, e);
        return null;
    }
}

function dataDateOrNow(value: string | null | undefined): string {
    return safeToIsoString(value ? new Date(value) : new Date())!;
}

function addCounts(total: SyncActionCounts | undefined, delta: SyncActionCounts | undefined): SyncActionCounts | undefined {
    if (!delta) {
        return total;
    }
    if (!total) {
        return { ...delta };
    }
    const sum = { ...total };
    for (const key of Object.keys(sum) as (keyof SyncActionCounts)[]) {
        sum[key] += delta[key] ?? 0;
    }
    return sum;
}

interface ResolveChunk {
    conflicts: SyncConflictResolveItem[];
    potentialUpdates: SyncPotentialUpdateResolveItem[];
}

/**
 * Splits the combined conflict + potential-update items into request chunks whose total base64
 * payload does not exceed {@link MAX_RESOLVE_REQUEST_SIZE}. Items are interleaved so neither list
 * is starved when one is much larger than the other. Mirrors the CLI's BuildResolveChunks.
 */
function buildResolveChunks(
    conflicts: SyncConflictResolveItem[],
    potentialUpdates: SyncPotentialUpdateResolveItem[]
): ResolveChunk[] {
    const chunks: ResolveChunk[] = [];
    let current: ResolveChunk = { conflicts: [], potentialUpdates: [] };
    let currentSize = 0;

    const startNewChunkIfFull = (itemSize: number) => {
        if (currentSize > 0 && currentSize + itemSize > MAX_RESOLVE_REQUEST_SIZE) {
            chunks.push(current);
            current = { conflicts: [], potentialUpdates: [] };
            currentSize = 0;
        }
    };

    // Interleave by index so a long conflict list doesn't defer all potential updates
    const maxIndex = Math.max(conflicts.length, potentialUpdates.length);
    for (let i = 0; i < maxIndex; i++) {
        if (i < conflicts.length) {
            const item = conflicts[i];
            startNewChunkIfFull(item.fileContentBase64.length);
            current.conflicts.push(item);
            currentSize += item.fileContentBase64.length;
        }

        if (i < potentialUpdates.length) {
            const item = potentialUpdates[i];
            startNewChunkIfFull(item.fileContentBase64.length);
            current.potentialUpdates.push(item);
            currentSize += item.fileContentBase64.length;
        }
    }

    if (current.conflicts.length > 0 || current.potentialUpdates.length > 0) {
        chunks.push(current);
    }

    return chunks;
}
