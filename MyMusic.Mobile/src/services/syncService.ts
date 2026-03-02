import {File} from 'expo-file-system';
import {activateKeepAwakeAsync, deactivateKeepAwake} from 'expo-keep-awake';
import {Alert} from 'react-native';
import {
    acknowledgeAction,
    checkSync,
    completeSync,
    getPendingActions,
    getSessionRecords,
    getSessions,
    recordChunk,
    startSync,
    uploadFile
} from '../api/sync';
import type {SyncFileInfoItem} from '../api/types';
import {type SyncProgress, useSyncStore} from '../stores/syncStore';
import {
    getChunkSize,
    getDeviceId,
    getExcludePatterns,
    getMusicExtensions,
    getRepositoryPath,
    setLastSyncAt
} from './configService';
import {scanFromDirectory} from './fileScanner';

export interface SyncResult {
    created: number;
    updated: number;
    skipped: number;
    downloaded: number;
    removed: number;
    failed: number;
    sessionId?: number;
}

export async function runSync(
    onProgress: (progress: Partial<SyncProgress>) => void
): Promise<SyncResult> {
    const deviceId = getDeviceId();
    const repositoryPath = getRepositoryPath();
    const musicExtensions = getMusicExtensions();
    const excludePatterns = getExcludePatterns();
    const chunkSize = getChunkSize();

    const syncStore = useSyncStore.getState();
    const options = syncStore.options;

    const result: SyncResult = {created: 0, updated: 0, skipped: 0, downloaded: 0, removed: 0, failed: 0};

    if (!deviceId || !repositoryPath) {
        throw new Error('Device not configured');
    }

    try {
        await activateKeepAwakeAsync();

        onProgress({phase: 'scanning', totalFiles: 0, processedFiles: 0});

        const files = await scanFromDirectory(repositoryPath, {
            extensions: musicExtensions,
            excludePatterns,
            basePath: repositoryPath,
        });

        onProgress({totalFiles: files.length, phase: 'upload'});

        const startResponse = await startSync(deviceId, {dryRun: options.dryRun, repositoryPath});
        const sessionId = startResponse.sessionId;

        const chunks = chunkArray(files, chunkSize);

        for (let i = 0; i < chunks.length; i++) {
            const chunk = chunks[i];

            const syncFiles: SyncFileInfoItem[] = chunk.map(f => ({
                path: f.relativePath,
                modifiedAt: f.modifiedAt,
                createdAt: f.createdAt,
            }));

            const syncResponse = await checkSync(deviceId, {files: syncFiles, force: options.force});

            for (const fileToCreate of syncResponse.toCreate) {
                if (options.dryRun) {
                    result.created++;
                } else {
                    try {
                        const file = chunk.find(f => f.relativePath === fileToCreate.path);
                        if (file) {
                            await uploadFile(
                                deviceId,
                                {
                                    uri: file.fullPath,
                                    name: fileToCreate.path.split('/').pop() || 'file',
                                    type: 'audio/mpeg'
                                },
                                fileToCreate.path,
                                fileToCreate.modifiedAt.toISOString(),
                                fileToCreate.createdAt.toISOString()
                            );
                            result.created++;
                        }
                    } catch (e) {
                        result.failed++;
                        console.error('Upload failed:', e);
                    }
                }

                onProgress({
                    processedFiles: result.created + result.updated + result.skipped + result.failed,
                    currentFile: formatFilePath(fileToCreate.path, repositoryPath),
                    created: result.created,
                    updated: result.updated,
                    skipped: result.skipped,
                    failed: result.failed,
                });
            }

            for (const fileToUpdate of syncResponse.toUpdate) {
                if (options.dryRun) {
                    result.updated++;
                } else {
                    try {
                        const file = chunk.find(f => f.relativePath === fileToUpdate.path);
                        if (file) {
                            await uploadFile(
                                deviceId,
                                {
                                    uri: file.fullPath,
                                    name: fileToUpdate.path.split('/').pop() || 'file',
                                    type: 'audio/mpeg'
                                },
                                fileToUpdate.path,
                                fileToUpdate.modifiedAt.toISOString(),
                                fileToUpdate.createdAt.toISOString()
                            );
                            result.updated++;
                        }
                    } catch (e) {
                        result.failed++;
                        console.error('Update failed:', e);
                    }
                }

                onProgress({
                    processedFiles: result.created + result.updated + result.skipped + result.failed,
                    currentFile: formatFilePath(fileToUpdate.path, repositoryPath),
                    created: result.created,
                    updated: result.updated,
                    skipped: result.skipped,
                    failed: result.failed,
                });
            }

            result.skipped += chunk.length - syncResponse.toCreate.length - syncResponse.toUpdate.length;

            const recordItems = chunk.map(f => ({
                filePath: f.relativePath,
                action: syncResponse.toCreate.some(c => c.path === f.relativePath)
                    ? 'Created'
                    : syncResponse.toUpdate.some(u => u.path === f.relativePath)
                        ? 'Updated'
                        : 'Skipped',
                source: 'Device',
                reason: syncResponse.toCreate.some(c => c.path === f.relativePath)
                    ? 'New file'
                    : syncResponse.toUpdate.some(u => u.path === f.relativePath)
                        ? 'File updated'
                        : 'Unchanged',
            }));

            await recordChunk(deviceId, sessionId, {records: recordItems});
        }

        onProgress({phase: 'server'});

        const pendingActions = await getPendingActions(deviceId);

        function confirmDeletion(filePath: string): Promise<boolean> {
            return new Promise((resolve) => {
                Alert.alert(
                    'Delete File?',
                    `Do you want to delete "${filePath}"?`,
                    [
                        {text: 'Skip', style: 'cancel', onPress: () => resolve(false)},
                        {text: 'Delete', style: 'destructive', onPress: () => resolve(true)},
                    ],
                    {cancelable: false}
                );
            });
        }

        for (const action of pendingActions.actions) {
            if (action.action === 'Download') {
                if (!options.dryRun) {
                    result.downloaded++;
                } else {
                    result.downloaded++;
                }
                if (!options.dryRun) {
                    await acknowledgeAction(deviceId, {songId: action.songId});
                }
            } else if (action.action === 'Remove') {
                const filePath = `${repositoryPath}/${action.path}`;
                const file = new File(filePath);

                if (!file.exists) {
                    onProgress({
                        removed: result.removed,
                        processedFiles: pendingActions.actions.indexOf(action) + 1,
                        totalFiles: pendingActions.actions.length,
                    });
                    continue;
                }

                let shouldDelete = true;

                if (!options.autoConfirm && !options.dryRun) {
                    shouldDelete = await confirmDeletion(action.path);
                }

                if (!shouldDelete) {
                    continue;
                }

                if (!options.dryRun) {
                    const file = new File(filePath);
                    await file.delete();
                    await acknowledgeAction(deviceId, {songId: action.songId});
                }
                await recordChunk(deviceId, sessionId, {
                    records: [{filePath: action.path, action: 'Removed', songId: action.songId}],
                });
                result.removed++;
            }

            onProgress({
                downloaded: result.downloaded,
                removed: result.removed,
                processedFiles: pendingActions.actions.indexOf(action) + 1,
                totalFiles: pendingActions.actions.length,
            });
        }

        onProgress({phase: 'completing'});

        const completeResponse = await completeSync(deviceId, sessionId);

        result.created = completeResponse.createdCount;
        result.updated = completeResponse.updatedCount;
        result.skipped = completeResponse.skippedCount;
        result.downloaded = completeResponse.downloadedCount;
        result.removed = completeResponse.removedCount;
        result.failed = completeResponse.errorCount;
        result.sessionId = sessionId;

        await setLastSyncAt(new Date().toISOString());

    } catch (error) {
        console.error('Sync error:', error);
        result.failed++;
        throw error;
    } finally {
        deactivateKeepAwake();
    }

    return result;
}

function chunkArray<T>(array: T[], size: number): T[][] {
    const chunks: T[][] = [];
    for (let i = 0; i < array.length; i += size) {
        chunks.push(array.slice(i, i + size));
    }
    return chunks;
}

function formatFilePath(path: string, repositoryPath: string): string {
    let formatted = path;

    if (repositoryPath && formatted.startsWith(repositoryPath)) {
        formatted = formatted.slice(repositoryPath.length);
    }

    if (formatted.startsWith('/')) {
        formatted = formatted.slice(1);
    }

    try {
        formatted = decodeURIComponent(formatted);
    } catch {
        // Keep original if decode fails
    }

    return formatted;
}

export async function fetchSyncHistory(deviceId: number, count: number = 10) {
    return getSessions(deviceId, count);
}

export async function fetchSessionDetails(deviceId: number, sessionId: number, actions?: string, source?: string) {
    return getSessionRecords(deviceId, sessionId, actions, source);
}