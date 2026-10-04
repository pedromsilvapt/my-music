import { Directory, File } from 'expo-file-system';
import { activateKeepAwakeAsync, deactivateKeepAwake } from 'expo-keep-awake';
import { Alert } from 'react-native';
import {
    acknowledgeAction,
    checkSync,
    chooseConflicts,
    commitSync,
    completeSync,
    downloadSong,
    createPendingActions,
    reportSyncError,
    resolveConflicts,
    prepareDeduplicate,
    startSync,
    uploadFile,
} from '../../api/sync';
import {
    getChunkSize,
    getDeviceId,
    getExcludePatterns,
    getLastScanTotal,
    getMusicExtensions,
    getRepositoryPath,
    setLastScanTotal,
    setLastSyncAt,
} from '../configService';
import { getDevice, updateDevice } from '../../api/devices';
import { getLocalDeviceOptions } from '../deviceConfigService';
import { getScanner } from '../scannerRegistry';
import { toFileUri } from '../pathUtils';
import { useSyncStore } from '../../stores/syncStore';
import { hashFile } from '../../../modules/xxhash';
import * as repoFiles from '../../../modules/repo-files';
import type {
    IFileOps,
    IFileSystemScanner,
    IKeepAwake,
    ISyncApiClient,
    ISyncConfig,
    ISyncState,
    IUserPrompt,
    ConflictResolution,
    SyncRecordItem,
} from './types';

export function createDefaultApiClient(): ISyncApiClient {
    return {
        getDevice,
        updateDevice,
        startSync,
        prepareDeduplicate,
        checkSync: async (deviceId, sessionId, request) => {
            const result = await checkSync(deviceId, sessionId, request);
            return {
                ...result,
                records: result.records as SyncRecordItem[],
            };
        },
        uploadFile: async (deviceId, sessionId, file, path, modifiedAt, createdAt, resolvesConflictRecordId) => {
            // The sync engine passes filesystem paths; React Native's FormData needs a URI
            const result = await uploadFile(deviceId, sessionId, { ...file, uri: toFileUri(file.uri) }, path, modifiedAt, createdAt, resolvesConflictRecordId);
            return {
                success: result.success,
                songId: result.songId,
                records: result.records as SyncRecordItem[],
                counts: result.counts,
            };
        },
        commitSync,
        completeSync,
        createPendingActions: async (deviceId, sessionId) => {
            const result = await createPendingActions(deviceId, sessionId);
            return {
                records: result.records as SyncRecordItem[],
            };
        },
        acknowledgeAction,
        resolveConflicts,
        chooseConflicts: async (deviceId, sessionId, request) => {
            const result = await chooseConflicts(deviceId, sessionId, request);
            return {
                ...result,
                records: result.records as SyncRecordItem[],
            };
        },
        downloadSong,
        reportSyncError,
    };
}

export function createDefaultConfig(): ISyncConfig {
    return {
        getDeviceId,
        getDeviceOptions: getLocalDeviceOptions,
        getRepositoryPath,
        getMusicExtensions,
        getExcludePatterns,
        getChunkSize,
        getLastScanTotal,
        setLastScanTotal,
        setLastSyncAt,
    };
}

export function createDefaultState(): ISyncState {
    const state = useSyncStore.getState();
    return {
        get isCancelled() {
            return useSyncStore.getState().isCancelled;
        },
        get options() {
            return useSyncStore.getState().options;
        },
    };
}

export function createDefaultScanner(scannerType: 'fileSystem' | 'mediaLibrary' = 'fileSystem'): IFileSystemScanner {
    return (directoryUri, options) => {
        const scanner = getScanner(scannerType);
        return scanner(directoryUri, options);
    };
}

/**
 * File operations of the sync. Reads go through expo-file-system; everything that writes to the
 * repository goes through the repo-files module, since expo-file-system rejects any target that
 * does not exist yet in shared storage.
 */
export function createDefaultFileOps(): IFileOps {
    return {
        fileExists: (path: string) => {
            return new File(toFileUri(path)).exists;
        },
        directoryExists: (path: string) => {
            return new Directory(toFileUri(path)).exists;
        },
        ensureDirectory: async (path: string) => {
            await repoFiles.ensureDirectory(path.substring(0, path.lastIndexOf('/')));
        },
        deleteFile: repoFiles.deleteFile,
        moveFile: repoFiles.moveFile,
        copyFile: repoFiles.copyFile,
        computeChecksum: async (path: string, algorithm: string) => {
            if (algorithm !== 'XxHash128') {
                throw new Error(`Unsupported checksum algorithm: ${algorithm}`);
            }
            return hashFile(toFileUri(path));
        },
        getModificationTime: (path: string) => {
            const info = new File(toFileUri(path));
            return info.modificationTime ? new Date(info.modificationTime) : null;
        },
        deleteEmptyDirectories: async (filePath: string, basePath: string) => {
            const file = new File(toFileUri(filePath));
            let dir = file.parentDirectory;
            const baseDir = new File(toFileUri(basePath));

            while (dir && dir.uri !== baseDir.uri) {
                if (!dir.exists) {
                    break;
                }

                const entries = await dir.list();
                if (entries && entries.length > 0) {
                    break;
                }

                await dir.delete();
                dir = dir.parentDirectory;
            }
        },
    };
}

export function createDefaultKeepAwake(): IKeepAwake {
    return {
        activate: async () => {
            await activateKeepAwakeAsync();
        },
        deactivate: () => {
            deactivateKeepAwake();
        },
    };
}

export function createDefaultUserPrompt(): IUserPrompt {
    return {
        promptConflictResolution: async (filePath: string, choices: ConflictResolution[]) => {
            return new Promise((resolve) => {
                Alert.alert(
                    'Conflict Detected',
                    `The file "${filePath}" has been modified both locally and on the server. What would you like to do?`,
                    [
                        ...(choices.includes('upload') ? [{ text: 'Upload', onPress: () => resolve('upload') }] : []),
                        ...(choices.includes('download') ? [{ text: 'Download', onPress: () => resolve('download') }] : []),
                        { text: 'Skip', style: 'destructive' as const, onPress: () => resolve('skip') },
                    ],
                    { cancelable: false }
                );
            });
        },
        confirmDeletion: async (filePath: string) => {
            return new Promise((resolve) => {
                Alert.alert(
                    'Delete File?',
                    `Do you want to delete "${filePath}"?`,
                    [
                        { text: 'Skip', style: 'cancel', onPress: () => resolve(false) },
                        { text: 'Delete', style: 'destructive', onPress: () => resolve(true) },
                    ],
                    { cancelable: false }
                );
            });
        },
    };
}
