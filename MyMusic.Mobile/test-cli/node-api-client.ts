import type {
    ISyncApiClient,
    PendingActionItem,
    SyncConflict,
} from '../src/services/sync/types';

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
        request: { dryRun?: boolean; repositoryPath?: string }
    ): Promise<{ sessionId: number }> {
        return this._post(`/devices/${deviceId}/sync/start`, request);
    }

    async checkSync(
        deviceId: number,
        request: {
            files: Array<{ path: string; modifiedAt: string; createdAt: string; reason?: string }>;
            force: boolean;
        }
    ): Promise<{
        toCreate: Array<{ path: string; modifiedAt: Date; createdAt: Date; reason?: string }>;
        toUpdate: Array<{ path: string; modifiedAt: Date; createdAt: Date; reason?: string }>;
        potentialConflicts: Array<{
            path: string;
            localModifiedAt: Date;
            serverModifiedAt: Date;
            lastSyncedAt: Date | null;
            songId: number;
            serverChecksum: string;
            serverChecksumAlgorithm: string;
        }>;
        pendingActions: PendingActionItem[];
    }> {
        const response = await this._post(`/devices/${deviceId}/sync/check`, request);
        return this._parseDates(response, ['toCreate', 'toUpdate', 'potentialConflicts']);
    }

    async uploadFile(
        deviceId: number,
        file: { uri: string; name: string },
        path: string,
        modifiedAt: string,
        createdAt: string
    ): Promise<{ success: boolean; songId: number; pendingActions: PendingActionItem[] }> {
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

        const response = await fetch(`${this._serverUrl}/devices/${deviceId}/sync/upload`, {
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

    async recordChunk(
        deviceId: number,
        sessionId: number,
        request: {
            records: Array<{
                filePath: string;
                action: string;
                source?: string;
                songId?: number;
                errorMessage?: string;
                reason?: string;
            }>;
        }
    ): Promise<{ success: boolean }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/records`, request);
    }

    async completeSync(
        deviceId: number,
        sessionId: number
    ): Promise<{
        createdCount: number;
        updatedCount: number;
        skippedCount: number;
        downloadedCount: number;
        removedCount: number;
        errorCount: number;
    }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/complete`, {});
    }

    async getPendingActions(deviceId: number): Promise<{ actions: PendingActionItem[] }> {
        return this._get(`/devices/${deviceId}/sync/pending-actions`);
    }

    async acknowledgeAction(
        deviceId: number,
        request: { devicePath: string; modifiedAt?: string; previousDevicePath?: string | null }
    ): Promise<{ success: boolean }> {
        return this._post(`/devices/${deviceId}/sync/acknowledge`, request);
    }

    async resolveConflicts(
        deviceId: number,
        request: {
            conflicts: Array<{
                path: string;
                songId: number;
                fileContentBase64: string;
                localModifiedAt: string;
            }>;
        }
    ): Promise<{
        toUpload: Array<{ path: string; modifiedAt: Date; createdAt: Date; reason?: string }>;
        resolved: Array<{ path: string; modifiedAt: Date; createdAt: Date; reason?: string }>;
        conflicts: SyncConflict[];
    }> {
        const response = await this._post(`/devices/${deviceId}/sync/resolve-conflicts`, request);
        return this._parseDates(response, ['toUpload', 'resolved']);
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
