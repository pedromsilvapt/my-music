import * as SecureStore from 'expo-secure-store';
import {DEFAULT_DEVICE_TYPE} from '../constants/deviceIcons';
import {useConfigStore} from '../stores/configStore';

const SECURE_USER_ID_KEY = 'mymusic-userId';
const SECURE_USER_NAME_KEY = 'mymusic-userName';

let isInitialized = false;

export async function initializeConfig(): Promise<void> {
    if (isInitialized) return;

    const storedUserId = await SecureStore.getItemAsync(SECURE_USER_ID_KEY);
    if (storedUserId) {
        useConfigStore.getState().setUserId(parseInt(storedUserId, 10));
    }

    const storedUserName = await SecureStore.getItemAsync(SECURE_USER_NAME_KEY);
    if (storedUserName) {
        useConfigStore.getState().setUserName(storedUserName);
    }

    isInitialized = true;
}

export function getServerUrl(): string {
    return useConfigStore.getState().serverUrl;
}

export async function setServerUrl(url: string): Promise<void> {
    const apiUrl = url.endsWith('/api') ? url : `${url}/api`;
    useConfigStore.getState().setServerUrl(apiUrl);
}

export function getDeviceName(): string {
    return useConfigStore.getState().deviceName;
}

export async function setDeviceName(name: string): Promise<void> {
    useConfigStore.getState().setDeviceName(name);
}

export function getDeviceIcon(): string {
    return useConfigStore.getState().deviceIcon;
}

export async function setDeviceIcon(icon: string): Promise<void> {
    useConfigStore.getState().setDeviceIcon(icon);
}

export function getDeviceId(): number | null {
    return useConfigStore.getState().deviceId;
}

export async function setDeviceId(id: number | null): Promise<void> {
    useConfigStore.getState().setDeviceId(id);
}

export function getImportOnPurchase(): boolean {
    return useConfigStore.getState().importOnPurchase;
}

export async function setImportOnPurchase(value: boolean): Promise<void> {
    useConfigStore.getState().setImportOnPurchase(value);
}

export function getRepositoryPath(): string {
    return useConfigStore.getState().repositoryPath;
}

export async function setRepositoryPath(path: string): Promise<void> {
    useConfigStore.getState().setRepositoryPath(path);
}

export function getMusicExtensions(): string[] {
    return ['.mp3'];
}

export function getExcludePatterns(): string[] {
    return ['**/.*', '**/Thumbs.db', '**/*.tmp', '**/desktop.ini'];
}

export function getChunkSize(): number {
    return 50;
}

export function getIsConfigured(): boolean {
    return useConfigStore.getState().isConfigured;
}

export async function setIsConfigured(configured: boolean): Promise<void> {
    useConfigStore.getState().setIsConfigured(configured);
}

export function getLastSyncAt(): string | null {
    return useConfigStore.getState().lastSyncAt;
}

export async function setLastSyncAt(date: string | null): Promise<void> {
    useConfigStore.getState().setLastSyncAt(date);
}

export function getUserId(): number | null {
    return useConfigStore.getState().userId;
}

export async function setUserId(id: number | null): Promise<void> {
    useConfigStore.getState().setUserId(id);
    if (id !== null) {
        await SecureStore.setItemAsync(SECURE_USER_ID_KEY, id.toString());
    } else {
        await SecureStore.deleteItemAsync(SECURE_USER_ID_KEY);
    }
}

export function getUserName(): string {
    return useConfigStore.getState().userName;
}

export async function setUserName(name: string): Promise<void> {
    useConfigStore.getState().setUserName(name);
    await SecureStore.setItemAsync(SECURE_USER_NAME_KEY, name);
}

export function getAllConfig() {
    const state = useConfigStore.getState();
    return {
        serverUrl: state.serverUrl,
        deviceName: state.deviceName,
        deviceIcon: state.deviceIcon,
        deviceId: state.deviceId,
        importOnPurchase: state.importOnPurchase,
        repositoryPath: state.repositoryPath,
        isConfigured: state.isConfigured,
        lastSyncAt: state.lastSyncAt,
        userId: state.userId,
        userName: state.userName,
    };
}

export async function resetConfig(): Promise<void> {
    useConfigStore.getState().setServerUrl('http://localhost:5000/api');
    useConfigStore.getState().setDeviceName('My Phone');
    useConfigStore.getState().setDeviceIcon(DEFAULT_DEVICE_TYPE.id);
    useConfigStore.getState().setDeviceId(null);
    useConfigStore.getState().setImportOnPurchase(false);
    useConfigStore.getState().setRepositoryPath('');
    useConfigStore.getState().setIsConfigured(false);
    useConfigStore.getState().setLastSyncAt(null);
    useConfigStore.getState().setUserId(null);
    useConfigStore.getState().setUserName('');

    await SecureStore.deleteItemAsync(SECURE_USER_ID_KEY);
    await SecureStore.deleteItemAsync(SECURE_USER_NAME_KEY);
}