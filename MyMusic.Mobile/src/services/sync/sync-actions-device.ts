import type {ISyncApiClient, IFileOps, IUserPrompt, SyncContext, SyncFileBase, ActionResult, ConflictResolution, ResolveConflictsResult, SyncActionCounts, ProgressHandler, SyncRecordItem} from './types';
import type {SyncConflictResolveItem, SyncPotentialUpdateResolveItem, RenameData, ConflictData, SongModifiedAtData} from '../../api/types';
import {safeToIsoString} from './utils';
import {excludedPathError} from './exclusions';

export async function actionCreateRemote(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    file: SyncFileBase,
    reason?: string
): Promise<ActionResult> {
    if (!fileOps.fileExists(file.fullPath)) {
        return reportUploadFailure(apiClient, ctx, file.relativePath, `File not found: ${file.fullPath}`, reason);
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
        return reportUploadFailure(apiClient, ctx, file.relativePath, errorMessage, reason);
    }
}

export async function actionUpdateRemote(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    file: SyncFileBase,
    reason?: string,
    resolvesConflictRecordId?: number
): Promise<ActionResult> {
    if (!fileOps.fileExists(file.fullPath)) {
        return reportUploadFailure(apiClient, ctx, file.relativePath, `File not found: ${file.fullPath}`, reason);
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
            safeToIsoString(file.createdAt)!,
            resolvesConflictRecordId
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
        return reportUploadFailure(apiClient, ctx, file.relativePath, errorMessage, reason);
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
    const excluded = await reportExcluded(apiClient, ctx, recordId, path, songId ?? undefined, `${reason ?? 'Server-initiated download'} failed`);
    if (excluded) {
        return excluded;
    }

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
    const excluded = await reportExcluded(apiClient, ctx, recordId, path, songId ?? undefined, `${reason ?? 'Server-initiated update'} failed`);
    if (excluded) {
        return excluded;
    }

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
                await apiClient.downloadSong(songId!, tempPath);
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

/** Whether the user lets a local file be deleted: the answer given for every deletion, or the one given now. */
async function confirmDeletion(userPrompt: IUserPrompt, ctx: SyncContext, path: string): Promise<boolean> {
    if (ctx.rememberedAnswers.deletion !== undefined) {
        return ctx.rememberedAnswers.deletion;
    }

    const answer = await userPrompt.confirmDeletion(path);
    if (answer.applyToAll) {
        ctx.rememberedAnswers.deletion = answer.value;
    }

    return answer.value;
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
    const excluded = await reportExcluded(apiClient, ctx, recordId, path, songId, `${reason ?? 'Server-initiated removal'} failed`);
    if (excluded) {
        return excluded;
    }

    const fullPath = `${decodedRepoPath}/${path}`;
    const fileExists = fileOps.fileExists(fullPath);

    if (!fileExists) {
        await apiClient.acknowledgeAction(ctx.deviceId, ctx.sessionId!, { recordIds: [recordId] });
        return null;
    }

    const baseReason = reason ?? 'Server-initiated removal';

    if (!ctx.options.autoConfirm && !ctx.options.dryRun) {
        const confirmed = await confirmDeletion(userPrompt, ctx, path);
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
    const excluded = await reportExcluded(apiClient, ctx, recordId, relativePath, undefined, `Rename from '${previousRelativePath}' failed`, previousRelativePath);
    if (excluded) {
        return excluded;
    }

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
            // A move replaces the target, so a file already there (one whose own rename or deletion
            // failed) would be lost. A rename that only changes the case finds the file itself
            if (fullPath.toLowerCase() !== previousFullPath.toLowerCase() && fileOps.fileExists(fullPath)) {
                console.error('File already exists during rename:', relativePath);
                return reportFailure(apiClient, ctx, recordId, relativePath, undefined, 'File already exists', `Rename from '${previousRelativePath}' failed`);
            }

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
 * Reports an action on a path that an exclusion rule matches as an `Error`, whether or not the file exists
 * and in a dry run as well: a sync never creates, changes, moves or deletes an excluded file. Returns null
 * when neither the path nor `otherPath` (the path a rename moves from) is excluded.
 */
async function reportExcluded(
    apiClient: ISyncApiClient,
    ctx: SyncContext,
    recordId: number,
    filePath: string,
    songId: number | undefined,
    reason: string,
    otherPath?: string
): Promise<ActionResult | null> {
    const rule = ctx.isExcluded(filePath) ?? (otherPath ? ctx.isExcluded(otherPath) : null);
    if (rule === null) {
        return null;
    }

    console.error(`Excluded path in a server action (rule '${rule}'):`, filePath);
    return reportFailure(apiClient, ctx, recordId, filePath, songId, excludedPathError(rule), reason);
}

/**
 * Reports a file that could not be uploaded as an `Error`, so the failure shows in the session.
 * The check does not save a record for an upload (the upload itself does), so there is none to link to.
 */
async function reportUploadFailure(
    apiClient: ISyncApiClient,
    ctx: SyncContext,
    filePath: string,
    errorMessage: string,
    reason: string | undefined
): Promise<ActionResult> {
    const result = await reportFailure(apiClient, ctx, undefined, filePath, undefined, errorMessage, reason);
    return { ...result, source: 'Device' };
}

/**
 * Reports a client action that could not be performed as an `Error` linked to its record.
 * The server acknowledges the record, so the commit is not blocked, and does not apply it, so
 * the server state keeps reflecting what is actually on the device.
 */
export async function reportFailure(
    apiClient: ISyncApiClient,
    ctx: SyncContext,
    recordId: number | undefined,
    filePath: string,
    songId: number | undefined,
    errorMessage: string,
    reason: string | undefined
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
        console.error(`Failed to report the error of ${recordId != null ? `record ${recordId}` : 'the upload'}: ${filePath}`, e);
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
    onProgress: ProgressHandler,
    files: SyncFileBase[] = []
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

        onProgress({ phase: 'resolving', currentFile: relativePath });
        const conflictData = conflict.data as ConflictData | null | undefined;
        const checksumAlgorithm = conflictData?.serverChecksumAlgorithm;
        const checksum = await computeLocalChecksum(ResolveItemKind.Conflict, fileOps, ctx, relativePath, checksumAlgorithm);
        if (checksum === null) {
            continue;
        }

        resolveItems.push({
            path: relativePath,
            songId: conflict.songId,
            checksum,
            checksumAlgorithm: checksumAlgorithm!,
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

        onProgress({ phase: 'resolving', currentFile: relativePath });
        const updateData = update.data as SongModifiedAtData | null | undefined;
        const checksumAlgorithm = updateData?.serverChecksumAlgorithm;
        const checksum = await computeLocalChecksum(ResolveItemKind.PotentialUpdate, fileOps, ctx, relativePath, checksumAlgorithm);
        if (checksum === null) {
            continue;
        }

        potentialUpdateItems.push({
            path: relativePath,
            songId: update.songId,
            checksum,
            checksumAlgorithm: checksumAlgorithm!,
            localModifiedAt: dataDateOrNow(updateData?.localModifiedAt),
            lastSyncedAt: dataDateOrNow(updateData?.lastSyncedAt),
        });
    }

    if (resolveItems.length === 0 && potentialUpdateItems.length === 0) {
        return { records: [], counts: undefined };
    }

    const pendingItems = interleaveResolveItems(resolveItems, potentialUpdateItems);
    console.log(`Resolving ${resolveItems.length} conflicts and ${potentialUpdateItems.length} potential updates`);

    const allRecords: SyncRecordItem[] = [];
    let aggregatedCounts: SyncActionCounts | undefined;

    try {
        const chunkSize = ctx.resolveChunkSize;

        for (let offset = 0; offset < pendingItems.length;) {
            const chunk = takeResolveChunk(pendingItems, offset, chunkSize.current);
            const chunkItemCount = chunk.conflicts.length + chunk.potentialUpdates.length;
            offset += chunkItemCount;

            const resolveStartedAt = Date.now();
            const resolveResponse = await apiClient.resolveConflicts(ctx.deviceId, ctx.sessionId!, chunk);
            chunkSize.report(chunkItemCount, Date.now() - resolveStartedAt);

            allRecords.push(...resolveResponse.records);
            aggregatedCounts = addCounts(aggregatedCounts, resolveResponse.counts);

            // Real conflicts are settled by the user: keep the local file (upload), take the server's
            // (download), or leave the conflict unresolved (skip)
            const downloadRecordIds: number[] = [];

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
                            const resolution = await chooseConflictResolution(userPrompt, ctx, record.filePath);
                            if (resolution === 'download') {
                                downloadRecordIds.push(record.id);
                            } else if (resolution === 'upload') {
                                const uploadResult = await uploadConflictedFile(apiClient, fileOps, ctx, record, files);
                                allRecords.push(...(uploadResult.records ?? []));
                                aggregatedCounts = addCounts(aggregatedCounts, uploadResult.counts);
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

            if (downloadRecordIds.length > 0) {
                const choicesResponse = await apiClient.chooseConflicts(ctx.deviceId, ctx.sessionId!, { downloadRecordIds });
                allRecords.push(...choicesResponse.records);
                aggregatedCounts = addCounts(aggregatedCounts, choicesResponse.counts);
            }

            // Report the files this request settled
            ctx.processedFiles += chunkItemCount;
            onProgress({ phase: 'resolving', processedFiles: ctx.processedFiles });
        }
    } catch (e) {
        console.error('Failed to resolve conflicts:', e);
    }

    return { records: allRecords, counts: aggregatedCounts };
}

/** The resolution of a real conflict: the user's answer, asked once when given for every conflict. */
async function chooseConflictResolution(userPrompt: IUserPrompt, ctx: SyncContext, filePath: string): Promise<ConflictResolution> {
    if (ctx.rememberedAnswers.conflict !== undefined) {
        return ctx.rememberedAnswers.conflict;
    }

    const answer = await userPrompt.promptConflictResolution(filePath, conflictChoices(ctx));
    if (answer.applyToAll) {
        ctx.rememberedAnswers.conflict = answer.value;
    }

    return answer.value;
}

/** The choices the user has for a real conflict: a direction that never changes one side cannot pick it. */
function conflictChoices(ctx: SyncContext): ConflictResolution[] {
    switch (ctx.options.direction) {
        case 'Up':
            return ['upload', 'skip'];
        case 'Down':
            return ['download', 'skip'];
        default:
            return ['upload', 'download', 'skip'];
    }
}

/**
 * Uploads the local file of a conflict the user resolved by keeping it. The records the server creates
 * for the file point to the conflict; a failed upload leaves it unresolved.
 */
async function uploadConflictedFile(
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    conflict: SyncRecordItem,
    files: SyncFileBase[]
): Promise<ActionResult> {
    const conflictData = conflict.data as ConflictData | null | undefined;
    const localModifiedAt = new Date(conflictData?.localModifiedAt ?? NaN);
    const file = files.find(f => f.relativePath === conflict.filePath) ?? {
        relativePath: conflict.filePath,
        fullPath: ctx.decodedRepoPath ? `${ctx.decodedRepoPath}/${conflict.filePath}` : conflict.filePath,
        modifiedAt: localModifiedAt,
        createdAt: localModifiedAt,
    };

    const result = await actionUpdateRemote(apiClient, fileOps, ctx, file, 'Conflict resolved by user: local version wins', conflict.id);

    if (result.action === 'Error') {
        console.error('Failed to upload the local version of', conflict.filePath, ':', result.errorMessage);
        // An error reported to the server comes back in the counts of the result
        if (!result.counts) {
            ctx.result.error++;
        }
    } else {
        ctx.uploadedPaths.add(conflict.filePath);
    }

    return result;
}

/**
 * Computes the checksum of a local file for conflict resolution, with the algorithm the server uses
 * for the song. Returns null, after logging, when the file is missing, cannot be read, or the
 * algorithm is absent or unknown, so the other items are still resolved.
 */
async function computeLocalChecksum(kind: ResolveItemKind, fileOps: IFileOps, ctx: SyncContext, relativePath: string, algorithm: string | null | undefined): Promise<string | null> {
    if (!algorithm) {
        console.error(`No checksum algorithm given for ${kind} resolution:`, relativePath);
        return null;
    }

    try {
        const fullPath = ctx.decodedRepoPath ? `${ctx.decodedRepoPath}/${relativePath}` : relativePath;
        if (!fileOps.fileExists(fullPath)) {
            console.error(`${kind} file not found locally:`, relativePath);
            return null;
        }

        return await fileOps.computeChecksum(fullPath, algorithm);
    } catch (e) {
        console.error(`Failed to compute the checksum for ${kind} resolution:`, relativePath, e);
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

/** An item of a resolve request: either a conflict or a potential update. */
type ResolveItem =
    | { conflict: SyncConflictResolveItem; potentialUpdate?: undefined }
    | { conflict?: undefined; potentialUpdate: SyncPotentialUpdateResolveItem };

/**
 * Orders the conflict + potential-update items for their requests. Items are interleaved so neither list
 * is starved when one is much larger than the other. Mirrors the CLI's InterleaveResolveItems.
 */
function interleaveResolveItems(
    conflicts: SyncConflictResolveItem[],
    potentialUpdates: SyncPotentialUpdateResolveItem[]
): ResolveItem[] {
    const items: ResolveItem[] = [];

    // Interleave by index so a long conflict list doesn't defer all potential updates
    const maxIndex = Math.max(conflicts.length, potentialUpdates.length);
    for (let i = 0; i < maxIndex; i++) {
        if (i < conflicts.length) {
            items.push({ conflict: conflicts[i] });
        }

        if (i < potentialUpdates.length) {
            items.push({ potentialUpdate: potentialUpdates[i] });
        }
    }

    return items;
}

/**
 * The next request chunk: at most `size` of the items from `offset` on. Each item only carries a checksum,
 * so the size just keeps a single request (one server transaction) bounded.
 */
function takeResolveChunk(items: ResolveItem[], offset: number, size: number): ResolveChunk {
    const chunk: ResolveChunk = { conflicts: [], potentialUpdates: [] };

    for (const item of items.slice(offset, offset + size)) {
        if (item.conflict) {
            chunk.conflicts.push(item.conflict);
        } else {
            chunk.potentialUpdates.push(item.potentialUpdate);
        }
    }

    return chunk;
}
