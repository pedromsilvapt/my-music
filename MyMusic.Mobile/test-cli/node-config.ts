import type { ISyncConfig } from '../src/services/sync/types';

interface NodeSyncConfigJson {
    deviceId: number;
    repositoryPath: string;
    serverUrl: string;
    userId: number;
    userName: string;
    musicExtensions: string[];
    excludePatterns: string[];
    chunkSize: number;
    lastScanTotal?: number;
    lastSyncAt?: string;
}

export class NodeSyncConfig implements ISyncConfig {
    private _config: NodeSyncConfigJson;

    constructor(configPath: string) {
        const fs = require('fs');
        const raw = fs.readFileSync(configPath, 'utf-8');
        this._config = JSON.parse(raw);
    }

    getDeviceId(): number | null {
        return this._config.deviceId ?? null;
    }

    getRepositoryPath(): string {
        return this._config.repositoryPath;
    }

    getMusicExtensions(): string[] {
        return this._config.musicExtensions ?? ['.mp3'];
    }

    getExcludePatterns(): string[] {
        return this._config.excludePatterns ?? ['**/.*', '**/Thumbs.db'];
    }

    getChunkSize(): number {
        return this._config.chunkSize ?? 50;
    }

    async getLastScanTotal(): Promise<number | null> {
        return this._config.lastScanTotal ?? null;
    }

    async setLastScanTotal(count: number): Promise<void> {
        this._config.lastScanTotal = count;
        this._save();
    }

    async setLastSyncAt(date: string): Promise<void> {
        this._config.lastSyncAt = date;
        this._save();
    }

    getServerUrl(): string {
        const url = this._config.serverUrl;
        return url.endsWith('/api') ? url : `${url}/api`;
    }

    getUserId(): number {
        return this._config.userId;
    }

    getUserName(): string {
        return this._config.userName;
    }

    private _save(): void {
        // No-op for test CLI; persistence not needed between syncs in tests
    }
}
