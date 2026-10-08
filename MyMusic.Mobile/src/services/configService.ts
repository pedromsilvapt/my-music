import * as SecureStore from 'expo-secure-store';
import AsyncStorage from '@react-native-async-storage/async-storage';
import {DEFAULT_DEVICE_TYPE} from '../constants/deviceIcons';
import {DEFAULT_EXCLUDE_PATTERNS, useConfigStore} from '../stores/configStore';
import {DEFAULT_CHUNK_TUNING} from './sync/adaptive-chunk-size';
import type {FileModifiedAtSource, SyncChunkTuning} from './sync/types';

const SECURE_USER_ID_KEY = 'mymusic-userId';
const SECURE_USER_NAME_KEY = 'mymusic-userName';
const LAST_SCAN_TOTAL_KEY = 'mymusic-lastScanTotal';

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

export function getNamingTemplate(): string {
    return useConfigStore.getState().namingTemplate;
}

export async function setNamingTemplate(template: string): Promise<void> {
    useConfigStore.getState().setNamingTemplate(template);
}

export function getMusicExtensions(): string[] {
    return ['.mp3'];
}

export function getExcludePatterns(): string[] {
    return useConfigStore.getState().excludePatterns;
}

export async function setExcludePatterns(patterns: string[]): Promise<void> {
    useConfigStore.getState().setExcludePatterns(patterns);
}

export function getChunkTuning(): SyncChunkTuning {
    return useConfigStore.getState().chunkTuning;
}

export async function setChunkTuning(tuning: SyncChunkTuning): Promise<void> {
    useConfigStore.getState().setChunkTuning(tuning);
}

export function getFileModifiedAt(): FileModifiedAtSource {
    return useConfigStore.getState().fileModifiedAt;
}

export async function setFileModifiedAt(source: FileModifiedAtSource): Promise<void> {
    useConfigStore.getState().setFileModifiedAt(source);
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
        namingTemplate: state.namingTemplate,
        excludePatterns: state.excludePatterns,
        chunkTuning: state.chunkTuning,
        fileModifiedAt: state.fileModifiedAt,
        isConfigured: state.isConfigured,
        lastSyncAt: state.lastSyncAt,
        userId: state.userId,
        userName: state.userName,
    };
}

export async function getLastScanTotal(): Promise<number | null> {
    const value = await AsyncStorage.getItem(LAST_SCAN_TOTAL_KEY);
    return value ? parseInt(value, 10) : null;
}

export async function setLastScanTotal(count: number): Promise<void> {
    await AsyncStorage.setItem(LAST_SCAN_TOTAL_KEY, count.toString());
}

export async function resetConfig(): Promise<void> {
    useConfigStore.getState().setServerUrl('http://localhost:5000/api');
    useConfigStore.getState().setDeviceName('My Phone');
    useConfigStore.getState().setDeviceIcon(DEFAULT_DEVICE_TYPE.id);
    useConfigStore.getState().setDeviceId(null);
    useConfigStore.getState().setImportOnPurchase(false);
    useConfigStore.getState().setRepositoryPath('');
    useConfigStore.getState().setNamingTemplate('');
    useConfigStore.getState().setExcludePatterns(DEFAULT_EXCLUDE_PATTERNS);
    useConfigStore.getState().setChunkTuning(DEFAULT_CHUNK_TUNING);
    useConfigStore.getState().setFileModifiedAt('Now');
    useConfigStore.getState().setIsConfigured(false);
    useConfigStore.getState().setLastSyncAt(null);
    useConfigStore.getState().setUserId(null);
    useConfigStore.getState().setUserName('');

    await SecureStore.deleteItemAsync(SECURE_USER_ID_KEY);
    await SecureStore.deleteItemAsync(SECURE_USER_NAME_KEY);
    await AsyncStorage.removeItem(LAST_SCAN_TOTAL_KEY);
}