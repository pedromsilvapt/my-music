import {File} from 'expo-file-system';
import {activateKeepAwakeAsync, deactivateKeepAwake} from 'expo-keep-awake';
import {Alert} from 'react-native';
import {
    acknowledgeAction,
    checkSync,
    completeSync,
    downloadSong,
    getPendingActions,
    getSessionRecords,
    getSessions,
    recordChunk,
    resolveConflicts,
    startSync,
    uploadFile
} from '../api/sync';
import type {SyncFileInfoItem, SyncConflictResolveItem, SyncConflictErrorItem, SyncPotentialConflictItem} from '../api/types';
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
    conflicts: number;
    sessionId?: number;
}

export type ConflictResolution = 'upload' | 'download' | 'skip';

async function promptConflictResolution(filePath: string): Promise<ConflictResolution> {
    return new Promise((resolve) => {
        Alert.alert(
            'Conflict Detected',
            `The file "${filePath}" has been modified both locally and on the server. What would you like to do?`,
            [
                {
                    text: 'Upload',
                    onPress: () => resolve('upload'),
                },
                {
                    text: 'Download',
                    onPress: () => resolve('download'),
                },
                {
                    text: 'Skip (Error)',
                    style: 'destructive',
                    onPress: () => resolve('skip'),
                },
            ],
            {cancelable: false}
        );
    });
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

    const result: SyncResult = {created: 0, updated: 0, skipped: 0, downloaded: 0, removed: 0, failed: 0, conflicts: 0};

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

            const toCreatePaths = new Set(syncResponse.toCreate.map(f => f.path));
            const toUpdatePaths = new Set(syncResponse.toUpdate.map(f => f.path));

            if (syncResponse.potentialConflicts.length > 0) {
                if (options.dryRun) {
                    console.log(
                        `Dry-run: ${syncResponse.potentialConflicts.length} potential conflicts detected (not resolved)`
                    );
                    result.conflicts += syncResponse.potentialConflicts.length;

                    onProgress({
                        phase: 'resolving',
                        currentFile: `${syncResponse.potentialConflicts.length} conflicts detected (dry-run)`,
                        conflicts: result.conflicts,
                    });
                } else {
                    onProgress({phase: 'resolving', currentFile: 'Checking conflicts...'});

                    const resolveItems: SyncConflictResolveItem[] = [];

                    for (const conflict of syncResponse.potentialConflicts) {
                        const file = chunk.find(f => f.relativePath === conflict.path);
                        if (!file) continue;

                        try {
                            const localFile = new File(file.fullPath);
                            const fileContentBase64 = await localFile.base64();
                            resolveItems.push({
                                path: conflict.path,
                                songId: conflict.songId,
                                fileContentBase64,
                                localModifiedAt: conflict.localModifiedAt,
                            });
                        } catch (e) {
                            console.error('Failed to read file for conflict resolution:', conflict.path, e);
                        }
                    }

                    if (resolveItems.length > 0) {
                        try {
                            const resolveResponse = await resolveConflicts(deviceId, {conflicts: resolveItems});

                            for (const resolved of resolveResponse.toUpload) {
                                toUpdatePaths.add(resolved.path);
                            }

                            for (const conflict of resolveResponse.conflicts) {
                                result.conflicts++;

                                if (options.treatConflictsAsErrors) {
                                    result.failed++;
                                    console.error('Conflict (as error):', conflict.path, conflict.reason);
                                } else {
                                    const resolution = await promptConflictResolution(conflict.path);

                                    if (resolution === 'upload') {
                                        toUpdatePaths.add(conflict.path);
                                    } else if (resolution === 'download') {
                                        const file = chunk.find(f => f.relativePath === conflict.path);
                                        if (file) {
                                            const conflictInfo = syncResponse.potentialConflicts.find(c => c.path === conflict.path);
                                            if (conflictInfo) {
                                                await handleDownloadConflict(
                                                    deviceId,
                                                    conflictInfo,
                                                    repositoryPath,
                                                    sessionId,
                                                    result,
                                                    options.dryRun
                                                );
                                                result.downloaded++;
                                            }
                                        }
                                    } else {
                                        result.failed++;
                                    }
                                }
                            }
                        } catch (e) {
                            console.error('Failed to resolve conflicts:', e);
                        }
                    }
                }
            }

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
                    processedFiles: result.created + result.updated + result.skipped + result.failed + result.conflicts,
                    currentFile: formatFilePath(fileToCreate.path, repositoryPath),
                    created: result.created,
                    updated: result.updated,
                    skipped: result.skipped,
                    failed: result.failed,
                    conflicts: result.conflicts,
                });
            }

            for (const fileToUpdate of syncResponse.toUpdate) {
                if (!toUpdatePaths.has(fileToUpdate.path)) {
                    continue;
                }

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
                    processedFiles: result.created + result.updated + result.skipped + result.failed + result.conflicts,
                    currentFile: formatFilePath(fileToUpdate.path, repositoryPath),
                    created: result.created,
                    updated: result.updated,
                    skipped: result.skipped,
                    failed: result.failed,
                    conflicts: result.conflicts,
                });
            }

            result.skipped += chunk.length - syncResponse.toCreate.length - syncResponse.toUpdate.length - syncResponse.potentialConflicts.length;

            const recordItems = chunk.map(f => {
                let action: string;
                let reason: string;

                if (toCreatePaths.has(f.relativePath)) {
                    action = 'Created';
                    reason = 'New file';
                } else if (toUpdatePaths.has(f.relativePath)) {
                    action = 'Updated';
                    reason = 'File updated';
                } else {
                    action = 'Skipped';
                    reason = 'Unchanged';
                }

                return {
                    filePath: f.relativePath,
                    action,
                    source: 'Device',
                    reason,
                };
            });

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

        const uploadedPaths = new Set<string>();

        for (let i = 0; i < chunks.length; i++) {
            const chunk = chunks[i];
            for (const file of chunk) {
                uploadedPaths.add(file.relativePath);
            }
        }

        for (const action of pendingActions.actions) {
            if (action.action === 'Download') {
                const fullPath = `${repositoryPath}/${action.path}`;
                const fileExists = new File(fullPath).exists;

                if (uploadedPaths.has(action.path)) {
                    if (!options.dryRun) {
                        const fileInfo = new File(fullPath);
                        await acknowledgeAction(deviceId, {
                            songId: action.songId,
                            modifiedAt: fileInfo.modificationTime ? new Date(fileInfo.modificationTime) : undefined,
                        });
                    }
                    result.downloaded++;
                } else {
                    if (!options.dryRun) {
                        try {
                            const blob = await downloadSong(action.songId);

                            const dir = new File(fullPath).parentDirectory;
                            if (dir && !dir.exists) {
                                dir.create();
                            }

                            const file = new File(fullPath);
                            const bytes = await blob.arrayBuffer();
                            await file.write(new Uint8Array(bytes));

                            const fileInfo = new File(fullPath);
                            const modifiedAt = fileInfo.modificationTime ? new Date(fileInfo.modificationTime) : undefined;

                            await acknowledgeAction(deviceId, {
                                songId: action.songId,
                                modifiedAt,
                            });

                            result.downloaded++;
                        } catch (e) {
                            result.failed++;
                            console.error('Download failed:', e);
                        }
                    } else {
                        result.downloaded++;
                    }
                }
            } else if (action.action === 'Remove') {
                const filePath = `${repositoryPath}/${action.path}`;
                const file = new File(filePath);

                if (!file.exists) {
                    if (!options.dryRun) {
                        await acknowledgeAction(deviceId, {songId: action.songId});
                    }
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

async function handleDownloadConflict(
    deviceId: number,
    conflict: SyncPotentialConflictItem,
    repositoryPath: string,
    sessionId: number,
    result: SyncResult,
    dryRun: boolean
) {
    const fullPath = `${repositoryPath}/${conflict.path}`;

    if (!dryRun) {
        try {
            const blob = await downloadSong(conflict.songId);

            const dir = new File(fullPath).parentDirectory;
            if (dir && !dir.exists) {
                dir.create();
            }

            const file = new File(fullPath);
            const bytes = await blob.arrayBuffer();
            await file.write(new Uint8Array(bytes));

            const fileInfo = new File(fullPath);
            const modifiedAt = fileInfo.modificationTime ? new Date(fileInfo.modificationTime) : undefined;

            await acknowledgeAction(deviceId, {
                songId: conflict.songId,
                modifiedAt,
            });

            await recordChunk(deviceId, sessionId, {
                records: [{
                    filePath: conflict.path,
                    action: 'Downloaded',
                    songId: conflict.songId,
                    source: 'Server',
                    reason: 'Conflict resolved: downloaded server version',
                }],
            });
        } catch (e) {
            console.error('Download conflict failed:', e);
            result.failed++;
        }
    }
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
    }

    return formatted;
}

export async function fetchSyncHistory(deviceId: number, count: number = 10) {
    return getSessions(deviceId, count);
}

export async function fetchSessionDetails(deviceId: number, sessionId: number, actions?: string, source?: string) {
    return getSessionRecords(deviceId, sessionId, actions, source);
}