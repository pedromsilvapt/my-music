import AsyncStorage from '@react-native-async-storage/async-storage';
import {create} from 'zustand';
import {createJSONStorage, persist} from 'zustand/middleware';
import {DEFAULT_DEVICE_TYPE} from '../constants/deviceIcons';
import {DEFAULT_CHUNK_TUNING} from '../services/sync/adaptive-chunk-size';
import type {SyncChunkTuning} from '../services/sync/types';

/** The exclusion rules of a new installation (see "Exclusion Rules" in docs/development/sync.md). */
export const DEFAULT_EXCLUDE_PATTERNS = ['**/.*', '**/Thumbs.db', '**/*.tmp', '**/desktop.ini'];

interface ConfigState {
    isLoading: boolean;
    serverUrl: string;
    deviceName: string;
    deviceIcon: string;
    deviceId: number | null;
    importOnPurchase: boolean;
    repositoryPath: string;
    namingTemplate: string;
    excludePatterns: string[];
    chunkTuning: SyncChunkTuning;
    isConfigured: boolean;
    lastSyncAt: string | null;
    userId: number | null;
    userName: string;
    setLoading: (loading: boolean) => void;
    setServerUrl: (url: string) => void;
    setDeviceName: (name: string) => void;
    setDeviceIcon: (icon: string) => void;
    setDeviceId: (id: number | null) => void;
    setImportOnPurchase: (value: boolean) => void;
    setRepositoryPath: (path: string) => void;
    setNamingTemplate: (template: string) => void;
    setExcludePatterns: (patterns: string[]) => void;
    setChunkTuning: (tuning: SyncChunkTuning) => void;
    setIsConfigured: (configured: boolean) => void;
    setLastSyncAt: (date: string | null) => void;
    setUserId: (id: number | null) => void;
    setUserName: (name: string) => void;
}

export const useConfigStore = create<ConfigState>()(
    persist(
        (set) => ({
            isLoading: true,
            serverUrl: 'http://localhost:5000/api',
            deviceName: 'My Phone',
            deviceIcon: DEFAULT_DEVICE_TYPE.id,
            deviceId: null,
            importOnPurchase: false,
            repositoryPath: '',
            namingTemplate: '',
            excludePatterns: DEFAULT_EXCLUDE_PATTERNS,
            chunkTuning: DEFAULT_CHUNK_TUNING,
            isConfigured: false,
            lastSyncAt: null,
            userId: null,
            userName: '',
            setLoading: (isLoading) => set({isLoading}),
            setServerUrl: (serverUrl) => set({serverUrl}),
            setDeviceName: (deviceName) => set({deviceName}),
            setDeviceIcon: (deviceIcon) => set({deviceIcon}),
            setDeviceId: (deviceId) => set({deviceId}),
            setImportOnPurchase: (importOnPurchase) => set({importOnPurchase}),
            setRepositoryPath: (repositoryPath) => set({repositoryPath}),
            setNamingTemplate: (namingTemplate) => set({namingTemplate}),
            setExcludePatterns: (excludePatterns) => set({excludePatterns}),
            setChunkTuning: (chunkTuning) => set({chunkTuning}),
            setIsConfigured: (isConfigured) => set({isConfigured}),
            setLastSyncAt: (lastSyncAt) => set({lastSyncAt}),
            setUserId: (userId) => set({userId}),
            setUserName: (userName) => set({userName}),
        }),
        {
            name: 'mymusic-config',
            storage: createJSONStorage(() => AsyncStorage),
            partialize: (state) => ({
                serverUrl: state.serverUrl,
                deviceName: state.deviceName,
                deviceIcon: state.deviceIcon,
                deviceId: state.deviceId,
                importOnPurchase: state.importOnPurchase,
                repositoryPath: state.repositoryPath,
                namingTemplate: state.namingTemplate,
                excludePatterns: state.excludePatterns,
                chunkTuning: state.chunkTuning,
                isConfigured: state.isConfigured,
                lastSyncAt: state.lastSyncAt,
                userId: state.userId,
                userName: state.userName,
            }),
        }
    )
);