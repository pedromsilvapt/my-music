import { DEFAULT_CHUNK_TUNING } from '../src/services/sync/adaptive-chunk-size';
import type { ChunkSizeRange, DeviceOptions, FileModifiedAtSource, ISyncConfig, SyncChunkTuning } from '../src/services/sync/types';

interface NodeSyncConfigJson {
    deviceId: number;
    deviceIcon?: string | null;
    namingTemplate?: string | null;
    importOnPurchase?: boolean;
    repositoryPath: string;
    serverUrl: string;
    userId: number;
    userName: string;
    musicExtensions: string[];
    excludePatterns: string[];
    /** Any part left out keeps its default. */
    chunkTuning?: Partial<Omit<SyncChunkTuning, 'check' | 'resolve'>> & { check?: Partial<ChunkSizeRange>; resolve?: Partial<ChunkSizeRange> };
    fileModifiedAt?: FileModifiedAtSource;
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

    getDeviceOptions(): DeviceOptions {
        return {
            icon: this._config.deviceIcon ?? null,
            namingTemplate: this._config.namingTemplate || null,
            importOnPurchase: this._config.importOnPurchase ?? false,
        };
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

    getChunkTuning(): SyncChunkTuning {
        const defaults = DEFAULT_CHUNK_TUNING;
        const tuning = this._config.chunkTuning ?? {};
        return {
            adaptive: tuning.adaptive ?? defaults.adaptive,
            targetRequestMs: tuning.targetRequestMs ?? defaults.targetRequestMs,
            check: { ...defaults.check, ...tuning.check },
            resolve: { ...defaults.resolve, ...tuning.resolve },
        };
    }

    getFileModifiedAt(): FileModifiedAtSource {
        return this._config.fileModifiedAt ?? 'Now';
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
