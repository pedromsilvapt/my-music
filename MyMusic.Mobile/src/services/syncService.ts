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
import type {SyncFileInfoItemRequest, SyncConflictResolveItem, SyncConflictErrorItem, SyncPotentialConflictItem} from '../api/types';

function safeToIsoString(date: Date | undefined): string | undefined {
    if (!date) return undefined;
    return isNaN(date.getTime()) ? new Date().toISOString() : date.toISOString();
}
import {type SyncProgress, useSyncStore} from '../stores/syncStore';
import {
    getChunkSize,
    getDeviceId,
    getExcludePatterns,
    getLastScanTotal,
    getMusicExtensions,
    getRepositoryPath,
    setLastScanTotal,
    setLastSyncAt
} from './configService';
import {type ScanResult} from './fileScanner';
import {getScanner} from './scannerRegistry';
import {decodeToFsPath, toFileUri} from './pathUtils';

export class SyncCancelledError extends Error {
    constructor() {
        super('Sync was cancelled');
        this.name = 'SyncCancelledError';
    }
}

export interface SyncResult {
    created: number;
    updated: number;
    skipped: number;
    downloaded: number;
    removed: number;
    failed: number;
    conflicts: number;
    sessionId?: number;
    cancelled?: boolean;
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

    const decodedRepoPath = decodeToFsPath(repositoryPath);

    try {
        await activateKeepAwakeAsync();

        // Load previous scan total for estimation
        const previousScanTotal = await getLastScanTotal();
        let estimatedTotal = previousScanTotal || 0;

        onProgress({
            phase: 'scanning',
            totalFiles: 0,
            estimatedTotalFiles: estimatedTotal,
            processedFiles: 0,
            scannedFiles: 0,
            currentFile: 'Scanning your music folder...'
        });

        const checkCancelled = () => {
            if (useSyncStore.getState().isCancelled) {
                throw new SyncCancelledError();
            }
        };

        const scanErrors: Array<{path: string; error: string}> = [];

        const {files, errors} = await getScanner(options.scannerType)(repositoryPath, {
            extensions: musicExtensions,
            excludePatterns,
            basePath: repositoryPath,
            onProgress: (scannedCount, currentDir) => {
                checkCancelled();
                // Extract a readable directory name from the full path
                const dirName = currentDir.split('/').pop() || 'music folder';
                
                // If actual count exceeds estimate, update estimate to match
                if (scannedCount > estimatedTotal) {
                    estimatedTotal = scannedCount;
                }
                
                onProgress({
                    scannedFiles: scannedCount,
                    estimatedTotalFiles: estimatedTotal,
                    currentFile: `${scannedCount} files found in ${dirName}...`
                });
            },
            onError: (path, error) => {
                scanErrors.push({path, error});
            },
        });

        // Add scan errors to the result count
        result.failed += errors.length;

        checkCancelled();

        // Update estimate one final time if actual is higher
        if (files.length > estimatedTotal) {
            estimatedTotal = files.length;
        }

        onProgress({
            totalFiles: files.length,
            estimatedTotalFiles: estimatedTotal,
            scannedFiles: files.length,
            phase: 'upload',
            currentFile: ''
        });

        const startResponse = await startSync(deviceId, {dryRun: options.dryRun, repositoryPath});
        const sessionId = startResponse.sessionId;

        // Record any scan errors to session history
        if (scanErrors.length > 0) {
            await recordChunk(deviceId, sessionId, {
                records: scanErrors.map(e => ({
                    filePath: e.path,
                    action: 'Error',
                    source: 'Device',
                    errorMessage: e.error,
                })),
            });
        }

        onProgress({phase: 'server'});

        const pendingActions = await getPendingActions(deviceId);

        const pendingDownloadPaths = new Set<string>();
        for (const action of pendingActions.actions) {
            if (action.action === 'Download') {
                pendingDownloadPaths.add(action.path);
            }
        }

        const chunks = chunkArray(files, chunkSize);

        for (let i = 0; i < chunks.length; i++) {
            checkCancelled();
            const chunk = chunks[i];
            const recordItems: Array<{
                filePath: string;
                action: string;
                source: string;
                reason?: string;
                errorMessage?: string;
            }> = [];

            const syncFiles: SyncFileInfoItemRequest[] = chunk.map(f => ({
                path: f.relativePath,
                modifiedAt: safeToIsoString(f.modifiedAt)!,
                createdAt: safeToIsoString(f.createdAt)!,
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
                            const localFile = new File(toFileUri(file.fullPath));
                            const fileContentBase64 = await localFile.base64();
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

                    if (resolveItems.length > 0) {
                        try {
                            const resolveResponse = await resolveConflicts(deviceId, {conflicts: resolveItems});

                            for (const resolved of resolveResponse.resolved) {
                                console.log('Resolved conflict for', resolved.path, ':', resolved.reason);
                            }

                            for (const toUploadItem of resolveResponse.toUpload) {
                                toUpdatePaths.add(toUploadItem.path);
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
                                                    decodedRepoPath,
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
                checkCancelled();

                if (options.dryRun) {
                    result.created++;
                    recordItems.push({
                        filePath: fileToCreate.path,
                        action: 'Created',
                        source: 'Device',
                        reason: fileToCreate.reason,
                    });
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
                                safeToIsoString(fileToCreate.modifiedAt)!,
                                safeToIsoString(fileToCreate.createdAt)!
                            );
                            result.created++;
                            recordItems.push({
                                filePath: fileToCreate.path,
                                action: 'Created',
                                source: 'Device',
                                reason: fileToCreate.reason,
                            });
                        }
                    } catch (e) {
                        result.failed++;
                        const errorMessage = e instanceof Error ? e.message : String(e);
                        console.error('Upload failed:', errorMessage);
                        recordItems.push({
                            filePath: fileToCreate.path,
                            action: 'Error',
                            source: 'Device',
                            reason: fileToCreate.reason,
                            errorMessage,
                        });
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
                checkCancelled();

                if (!toUpdatePaths.has(fileToUpdate.path)) {
                    continue;
                }

                if (options.dryRun) {
                    result.updated++;
                    recordItems.push({
                        filePath: fileToUpdate.path,
                        action: 'Updated',
                        source: 'Device',
                        reason: fileToUpdate.reason,
                    });
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
                                safeToIsoString(fileToUpdate.modifiedAt)!,
                                safeToIsoString(fileToUpdate.createdAt)!
                            );
                            result.updated++;
                            recordItems.push({
                                filePath: fileToUpdate.path,
                                action: 'Updated',
                                source: 'Device',
                                reason: fileToUpdate.reason,
                            });
                        }
                    } catch (e) {
                        result.failed++;
                        const errorMessage = e instanceof Error ? e.message : String(e);
                        console.error('Update failed:', errorMessage);
                        recordItems.push({
                            filePath: fileToUpdate.path,
                            action: 'Error',
                            source: 'Device',
                            reason: fileToUpdate.reason,
                            errorMessage,
                        });
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

            let skippedInChunk = 0;

            for (const file of chunk) {
                const inCreate = toCreatePaths.has(file.relativePath);
                const inUpdate = toUpdatePaths.has(file.relativePath);
                const isPendingDownload = pendingDownloadPaths.has(file.relativePath);

                if (!inCreate && !inUpdate && !isPendingDownload) {
                    skippedInChunk++;
                    recordItems.push({
                        filePath: file.relativePath,
                        action: 'Skipped',
                        source: 'Device',
                        reason: 'Unchanged',
                    });
                }
            }

            result.skipped += skippedInChunk;

            await recordChunk(deviceId, sessionId, {records: recordItems});
        }

        checkCancelled();

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

        const downloadRecordItems: Array<{
            filePath: string;
            action: string;
            source: string;
            songId?: number;
            reason?: string;
            errorMessage?: string;
        }> = [];

        for (const action of pendingActions.actions) {
            checkCancelled();

            if (action.action === 'Download') {
                const fullPath = `${decodedRepoPath}/${action.path}`;
                const fileExists = new File(toFileUri(fullPath)).exists;

                if (!options.dryRun) {
                    try {
                        const dir = new File(toFileUri(fullPath)).parentDirectory;
                        if (dir && !dir.exists) {
                            dir.create();
                        }

                        if (fileExists) {
                            await new File(toFileUri(fullPath)).delete();
                        }

                        const blob = await downloadSong(action.songId!);

                        const file = new File(toFileUri(fullPath));
                        const bytes = await blob.arrayBuffer();
                        await file.write(new Uint8Array(bytes));

                        const fileInfo = new File(toFileUri(fullPath));
                        const modifiedAt = fileInfo.modificationTime ? safeToIsoString(new Date(fileInfo.modificationTime)) : undefined;

                        await acknowledgeAction(deviceId, {
                            devicePath: action.path,
                            modifiedAt,
                        });

                        result.downloaded++;
                        downloadRecordItems.push({
                            filePath: action.path,
                            action: 'Downloaded',
                            source: 'Server',
                            songId: action.songId ?? undefined,
                            reason: 'Server-initiated download',
                        });
                    } catch (e) {
                        result.failed++;
                        const errorMessage = e instanceof Error ? e.message : String(e);
                        console.error('Download failed:', errorMessage);
                        downloadRecordItems.push({
                            filePath: action.path,
                            action: 'Error',
                            source: 'Server',
                            songId: action.songId ?? undefined,
                            reason: 'Server-initiated download failed',
                            errorMessage,
                        });
                    }
                } else {
                    result.downloaded++;
                    downloadRecordItems.push({
                        filePath: action.path,
                        action: 'Downloaded',
                        source: 'Server',
                        songId: action.songId ?? undefined,
                        reason: 'Server-initiated download',
                    });
                }
            } else if (action.action === 'Remove') {
                const filePath = `${decodedRepoPath}/${action.path}`;
                const file = new File(toFileUri(filePath));

                if (!file.exists) {
                    if (!options.dryRun) {
                        await acknowledgeAction(deviceId, {devicePath: action.path});
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
                    await acknowledgeAction(deviceId, {devicePath: action.path});
                }
                downloadRecordItems.push({
                    filePath: action.path,
                    action: 'Removed',
                    source: 'Server',
                    songId: action.songId ?? undefined,
                    reason: 'Server-initiated removal',
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

        if (downloadRecordItems.length > 0) {
            await recordChunk(deviceId, sessionId, {records: downloadRecordItems});
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
        
        // Save the actual scan count for next sync's estimate
        await setLastScanTotal(files.length);

    } catch (error) {
        if (error instanceof SyncCancelledError) {
            console.log('Sync cancelled by user');
            result.cancelled = true;
            return result;
        }
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
    decodedRepoPath: string,
    sessionId: number,
    result: SyncResult,
    dryRun: boolean
) {
    const fullPath = `${decodedRepoPath}/${conflict.path}`;

    if (!dryRun) {
        try {
            const dir = new File(toFileUri(fullPath)).parentDirectory;
            if (dir && !dir.exists) {
                dir.create();
            }

            const fileExists = new File(toFileUri(fullPath)).exists;
            if (fileExists) {
                await new File(toFileUri(fullPath)).delete();
            }

            const blob = await downloadSong(conflict.songId);

            const file = new File(toFileUri(fullPath));
            const bytes = await blob.arrayBuffer();
            await file.write(new Uint8Array(bytes));

            const fileInfo = new File(toFileUri(fullPath));
            const modifiedAt = fileInfo.modificationTime ? safeToIsoString(new Date(fileInfo.modificationTime)) : undefined;

            await acknowledgeAction(deviceId, {
                devicePath: conflict.path,
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

export async function fetchSessionDetails(deviceId: number, sessionId: number, actions?: string, source?: string, limit?: number, offset?: number | null, sort?: string) {
    return getSessionRecords(deviceId, sessionId, actions, source, limit, offset, sort);
}
