import { File } from 'expo-file-system';
import { activateKeepAwakeAsync, deactivateKeepAwake } from 'expo-keep-awake';
import { Alert } from 'react-native';
import {
    acknowledgeAction,
    checkSync,
    completeSync,
    downloadSong,
    getPendingActions,
    recordChunk,
    resolveConflicts,
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
import { getScanner } from '../scannerRegistry';
import { toFileUri } from '../pathUtils';
import { useSyncStore } from '../../stores/syncStore';
import type {
    IFileOps,
    IFileSystemScanner,
    IKeepAwake,
    ISyncApiClient,
    ISyncConfig,
    ISyncState,
    IUserPrompt,
} from './types';

export function createDefaultApiClient(): ISyncApiClient {
    return {
        startSync,
        checkSync,
        uploadFile,
        recordChunk,
        completeSync,
        getPendingActions,
        acknowledgeAction,
        resolveConflicts,
        downloadSong,
    };
}

export function createDefaultConfig(): ISyncConfig {
    return {
        getDeviceId,
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

export function createDefaultFileOps(): IFileOps {
    return {
        fileExists: (path: string) => {
            return new File(toFileUri(path)).exists;
        },
        ensureDirectory: async (path: string) => {
            const file = new File(toFileUri(path));
            const dir = file.parentDirectory;
            if (dir && !dir.exists) {
                dir.create();
            }
        },
        writeFile: async (path: string, data: Blob) => {
            const file = new File(toFileUri(path));
            const bytes = new Uint8Array(await data.arrayBuffer());
            await file.write(bytes);
        },
        deleteFile: async (path: string) => {
            await new File(toFileUri(path)).delete();
        },
        readFileBase64: async (path: string) => {
            return new File(toFileUri(path)).base64();
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
        promptConflictResolution: async (filePath: string) => {
            return new Promise((resolve) => {
                Alert.alert(
                    'Conflict Detected',
                    `The file "${filePath}" has been modified both locally and on the server. What would you like to do?`,
                    [
                        { text: 'Upload', onPress: () => resolve('upload') },
                        { text: 'Download', onPress: () => resolve('download') },
                        { text: 'Skip (Error)', style: 'destructive', onPress: () => resolve('skip') },
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
