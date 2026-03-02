import AsyncStorage from '@react-native-async-storage/async-storage';
import {create} from 'zustand';
import {createJSONStorage, persist} from 'zustand/middleware';
import {DEFAULT_DEVICE_TYPE} from '../constants/deviceIcons';

interface ConfigState {
    isLoading: boolean;
    serverUrl: string;
    deviceName: string;
    deviceIcon: string;
    deviceId: number | null;
    repositoryPath: string;
    isConfigured: boolean;
    lastSyncAt: string | null;
    userId: number | null;
    userName: string;
    setLoading: (loading: boolean) => void;
    setServerUrl: (url: string) => void;
    setDeviceName: (name: string) => void;
    setDeviceIcon: (icon: string) => void;
    setDeviceId: (id: number | null) => void;
    setRepositoryPath: (path: string) => void;
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
            repositoryPath: '',
            isConfigured: false,
            lastSyncAt: null,
            userId: null,
            userName: '',
            setLoading: (isLoading) => set({isLoading}),
            setServerUrl: (serverUrl) => set({serverUrl}),
            setDeviceName: (deviceName) => set({deviceName}),
            setDeviceIcon: (deviceIcon) => set({deviceIcon}),
            setDeviceId: (deviceId) => set({deviceId}),
            setRepositoryPath: (repositoryPath) => set({repositoryPath}),
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
                repositoryPath: state.repositoryPath,
                isConfigured: state.isConfigured,
                lastSyncAt: state.lastSyncAt,
                userId: state.userId,
                userName: state.userName,
            }),
        }
    )
);