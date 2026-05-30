import type {
    ISyncApiClient,
    SyncRecordItem,
    SyncActionCounts,
    SyncConflict,
} from '../src/services/sync/types';
import {SyncRecordItemSchema} from '../src/api/types';

export class NodeApiClient implements ISyncApiClient {
    private _serverUrl: string;
    private _userId: number;
    private _userName: string;

    constructor(serverUrl: string, userId: number, userName: string) {
        this._serverUrl = serverUrl.replace(/\/$/, '');
        this._userId = userId;
        this._userName = userName;
    }

    private _headers(): Record<string, string> {
        return {
            'Content-Type': 'application/json',
            'X-MyMusic-UserId': this._userId.toString(),
            'X-MyMusic-UserName': this._userName,
        };
    }

    private _parseRecords(records: unknown[]): SyncRecordItem[] {
        return records.map(r => {
            const parsed = SyncRecordItemSchema.safeParse(r);
            return parsed.success ? parsed.data : r as SyncRecordItem;
        });
    }

    private async _post<T>(endpoint: string, body: unknown): Promise<T> {
        const response = await fetch(`${this._serverUrl}${endpoint}`, {
            method: 'POST',
            headers: this._headers(),
            body: JSON.stringify(body),
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`API error ${response.status} on ${endpoint}: ${text}`);
        }

        return response.json();
    }

    private async _get<T>(endpoint: string): Promise<T> {
        const response = await fetch(`${this._serverUrl}${endpoint}`, {
            method: 'GET',
            headers: this._headers(),
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`API error ${response.status} on ${endpoint}: ${text}`);
        }

        return response.json();
    }

    async startSync(
        deviceId: number,
        request: { dryRun?: boolean; repositoryPath?: string; scanErrors?: Array<{ path: string; error: string }> }
    ): Promise<{ sessionId: number }> {
        return this._post(`/devices/${deviceId}/sync/start`, request);
    }

    async checkSync(
        deviceId: number,
        sessionId: number,
        request: {
            files: Array<{ path: string; modifiedAt: string; createdAt: string; reason?: string }>;
            force: boolean;
        }
    ): Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }> {
        const response = await this._post(`/devices/${deviceId}/sync/${sessionId}/check`, request);
        const records = this._parseRecords(response.records ?? []);
        return {
            records,
            counts: response.counts ?? { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 },
        };
    }

    async uploadFile(
        deviceId: number,
        sessionId: number,
        file: { uri: string; name: string },
        path: string,
        modifiedAt: string,
        createdAt: string
    ): Promise<{ success: boolean; songId: number | null; recordId: number | null; action: string | null; data: any; counts: SyncActionCounts }> {
        const fs = require('fs');
        const buffer = fs.readFileSync(file.uri);
        const blob = new Blob([buffer]);

        const formData = new FormData();
        formData.append('file', blob, file.name);
        formData.append('path', path);
        formData.append('modifiedAt', modifiedAt);
        formData.append('createdAt', createdAt);

        const headers = this._headers();
        delete headers['Content-Type'];

        const response = await fetch(`${this._serverUrl}/devices/${deviceId}/sync/${sessionId}/upload`, {
            method: 'POST',
            headers,
            body: formData,
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`API error ${response.status} on upload: ${text}`);
        }

        return response.json();
    }

    async commitSync(
        deviceId: number,
        sessionId: number,
        request?: { direction?: string }
    ): Promise<{
        createRemoteCount: number;
        updateRemoteCount: number;
        skippedCount: number;
        createLocalCount: number;
        updateLocalCount: number;
        deleteCount: number;
        linkCount: number;
        unlinkCount: number;
        renameCount: number;
        conflictCount: number;
        updateTimestampCount: number;
        errorCount: number;
        committedAt: Date;
    }> {
        const response: any = await this._post(`/devices/${deviceId}/sync/${sessionId}/commit`, request ?? {});
        if (response.committedAt && typeof response.committedAt === 'string') {
            response.committedAt = new Date(response.committedAt);
        }
        return response;
    }

    async completeSync(
        deviceId: number,
        sessionId: number
    ): Promise<{
        createRemoteCount: number;
        updateRemoteCount: number;
        skippedCount: number;
        createLocalCount: number;
        updateLocalCount: number;
        deleteCount: number;
        linkCount: number;
        unlinkCount: number;
        renameCount: number;
        conflictCount: number;
        updateTimestampCount: number;
        errorCount: number;
    }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/complete`, {});
    }

    async createPendingActions(deviceId: number, sessionId: number): Promise<{ records: SyncRecordItem[] }> {
        const response = await this._post<{ records: unknown[] }>(`/devices/${deviceId}/sync/${sessionId}/pending-actions`, {});
        return { records: this._parseRecords(response.records) };
    }

    async acknowledgeAction(
        deviceId: number,
        sessionId: number,
        request: { recordIds: number[]; modifiedAt?: string }
    ): Promise<{ success: boolean; counts: SyncActionCounts }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/acknowledge`, request);
    }

    async resolveConflicts(
        deviceId: number,
        sessionId: number,
        request: {
            conflicts: Array<{
                path: string;
                songId: number | null;
                fileContentBase64: string;
                localModifiedAt: string;
            }>;
            potentialUpdates: Array<{
                path: string;
                songId: number;
                fileContentBase64: string;
                localModifiedAt: string;
                lastSyncedAt: string;
            }>;
        }
    ): Promise<{
        records: Array<{ id: number; filePath: string; action: string; songId: number | null; data?: any; resolvesConflictRecordId?: number | null; reason?: string; acknowledged: boolean; processedAt: string }>;
        counts: SyncActionCounts;
    }> {
        const response = await this._post(`/devices/${deviceId}/sync/${sessionId}/resolve-conflicts`, request);
        return this._parseDates(response, ['records']);
    }

    async downloadSong(songId: number): Promise<Blob> {
        const response = await fetch(`${this._serverUrl}/songs/${songId}/download`, {
            method: 'GET',
            headers: this._headers(),
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`API error ${response.status} on download: ${text}`);
        }

        return response.blob();
    }

    async reportSyncError(
        deviceId: number,
        sessionId: number,
        request: { filePath: string; errorMessage: string; songId?: number | null }
    ): Promise<{ counts: SyncActionCounts }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/error`, request);
    }

    private _parseDates(response: any, fields: string[]): any {
        for (const field of fields) {
            if (Array.isArray(response[field])) {
                response[field] = response[field].map((item: any) => {
                    if (item.modifiedAt && typeof item.modifiedAt === 'string') {
                        item.modifiedAt = new Date(item.modifiedAt);
                    }
                    if (item.createdAt && typeof item.createdAt === 'string') {
                        item.createdAt = new Date(item.createdAt);
                    }
                    if (item.localModifiedAt && typeof item.localModifiedAt === 'string') {
                        item.localModifiedAt = new Date(item.localModifiedAt);
                    }
                    if (item.serverModifiedAt && typeof item.serverModifiedAt === 'string') {
                        item.serverModifiedAt = new Date(item.serverModifiedAt);
                    }
                    if (item.lastSyncedAt && typeof item.lastSyncedAt === 'string') {
                        item.lastSyncedAt = new Date(item.lastSyncedAt);
                    }
                    return item;
                });
            }
        }
        return response;
    }
}
