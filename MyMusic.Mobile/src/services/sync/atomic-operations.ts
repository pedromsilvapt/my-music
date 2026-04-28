import type { ISyncApiClient, IFileOps, IUserPrompt, RecordItem, SyncContext, SyncFileBase, SyncRecordAction } from './types';
import { safeToIsoString } from './utils';

export async function uploadOneFile (
    apiClient: ISyncApiClient,
    ctx: SyncContext,
    file: SyncFileBase,
    action: SyncRecordAction,
    reason?: string
): Promise<RecordItem> {
    if (ctx.options.dryRun) {
        ctx.result[action === 'Created' ? 'created' : 'updated']++;
        return {
            filePath: file.relativePath,
            action,
            source: 'Device',
            reason,
        };
    }

    try {
        const filename = file.relativePath.split('/').pop();
        if (!filename) {
            throw new Error(`Invalid file path: relativePath is empty for ${file.fullPath}`);
        }

        await apiClient.uploadFile(
            ctx.deviceId,
            {
                uri: file.fullPath,
                name: filename,
            },
            file.relativePath,
            safeToIsoString(file.modifiedAt)!,
            safeToIsoString(file.createdAt)!
        );
        ctx.result[action === 'Created' ? 'created' : 'updated']++;
        return {
            filePath: file.relativePath,
            action,
            source: 'Device',
            reason,
        };
    } catch (e) {
        ctx.result.failed++;
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            filePath: file.relativePath,
            action: 'Error',
            source: 'Device',
            reason,
            errorMessage,
        };
    }
}

export async function downloadOneFile (
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    ctx: SyncContext,
    songId: number,
    path: string,
    decodedRepoPath: string,
    previousPath?: string | null
): Promise<RecordItem | null> {
    const fullPath = `${decodedRepoPath}/${path}`;
    const previousFullPath = previousPath ? `${decodedRepoPath}/${previousPath}` : null;

    if (ctx.options.dryRun) {
        ctx.result.downloaded++;
        return {
            filePath: path,
            action: 'Downloaded',
            source: 'Server',
            songId,
            reason: 'Server-initiated download',
        };
    }

    try {
        await fileOps.ensureDirectory(fullPath);

        if (fileOps.fileExists(fullPath)) {
            await fileOps.deleteFile(fullPath);
        }

        const blob = await apiClient.downloadSong(songId);
        await fileOps.writeFile(fullPath, blob);

        if (previousFullPath && fileOps.fileExists(previousFullPath)) {
            await fileOps.deleteFile(previousFullPath);
            await fileOps.deleteEmptyDirectories(previousFullPath, decodedRepoPath);
        }

        const modifiedAt = fileOps.getModificationTime(fullPath);
        await apiClient.acknowledgeAction(ctx.deviceId, {
            devicePath: path,
            modifiedAt: modifiedAt ? safeToIsoString(modifiedAt) : undefined,
            previousDevicePath: previousPath,
        });

        ctx.result.downloaded++;
        return {
            filePath: path,
            action: 'Downloaded',
            source: 'Server',
            songId,
            reason: previousPath ? 'Server-initiated rename' : 'Server-initiated download',
        };
    } catch (e) {
        ctx.result.failed++;
        const errorMessage = e instanceof Error ? e.message : String(e);
        return {
            filePath: path,
            action: 'Error',
            source: 'Server',
            songId,
            reason: 'Server-initiated download failed',
            errorMessage,
        };
    }
}

export async function removeOneFile (
    apiClient: ISyncApiClient,
    fileOps: IFileOps,
    userPrompt: IUserPrompt,
    ctx: SyncContext,
    path: string,
    decodedRepoPath: string
): Promise<RecordItem | null> {
    const fullPath = `${decodedRepoPath}/${path}`;

    if (!fileOps.fileExists(fullPath)) {
        if (!ctx.options.dryRun) {
            await apiClient.acknowledgeAction(ctx.deviceId, { devicePath: path });
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

    if (!ctx.options.dryRun) {
        await fileOps.deleteFile(fullPath);
        await apiClient.acknowledgeAction(ctx.deviceId, { devicePath: path });
    }

    ctx.result.removed++;
    return {
        filePath: path,
        action: 'Removed',
        source: 'Server',
        reason: 'Server-initiated removal',
    };
}
