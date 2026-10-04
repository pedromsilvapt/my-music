import type {
    ISyncApiClient,
    SyncRecordItem,
    SyncActionCounts,
    SyncDirection,
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

    async getDevice(deviceId: number): Promise<{
        device: { icon: string | null; color: string | null; namingTemplate: string | null; importOnPurchase: boolean };
    }> {
        return this._get(`/devices/${deviceId}`);
    }

    async updateDevice(
        deviceId: number,
        request: { icon?: string; color?: string; namingTemplate?: string; importOnPurchase?: boolean }
    ): Promise<unknown> {
        const endpoint = `/devices/${deviceId}`;
        const response = await fetch(`${this._serverUrl}${endpoint}`, {
            method: 'PUT',
            headers: this._headers(),
            body: JSON.stringify(request),
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`API error ${response.status} on ${endpoint}: ${text}`);
        }

        return response.json();
    }

    async startSync(
        deviceId: number,
        request: { dryRun?: boolean; direction?: SyncDirection; repositoryPath?: string; deduplicate?: boolean; scanErrors?: Array<{ path: string; error: string }>; deviceOptions?: { namingTemplate: string | null } }
    ): Promise<{ sessionId: number }> {
        return this._post(`/devices/${deviceId}/sync/start`, request);
    }

    async prepareDeduplicate(
        deviceId: number,
        sessionId: number
    ): Promise<{ total: number; processed: number; done: boolean }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/deduplicate/prepare`, {});
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
        const response = await this._post<{ records?: unknown[]; counts?: SyncActionCounts }>(`/devices/${deviceId}/sync/${sessionId}/check`, request);
        const records = this._parseRecords(response.records ?? []);
        return {
            records,
            counts: response.counts ?? { createRemoteCount: 0, updateRemoteCount: 0, skippedCount: 0, createLocalCount: 0, updateLocalCount: 0, deleteLocalCount: 0, linkCount: 0, unlinkCount: 0, renameCount: 0, conflictCount: 0, updateTimestampCount: 0, errorCount: 0 },
        };
    }

    async uploadFile(
        deviceId: number,
        sessionId: number,
        file: { uri: string; name: string },
        path: string,
        modifiedAt: string,
        createdAt: string,
        resolvesConflictRecordId?: number
    ): Promise<{ success: boolean; songId: number | null; records: SyncRecordItem[]; counts: SyncActionCounts }> {
        const fs = require('fs');
        const buffer = fs.readFileSync(file.uri);
        const blob = new Blob([buffer]);

        const formData = new FormData();
        formData.append('file', blob, file.name);
        formData.append('path', path);
        formData.append('modifiedAt', modifiedAt);
        formData.append('createdAt', createdAt);
        if (resolvesConflictRecordId != null) {
            formData.append('resolvesConflictRecordId', String(resolvesConflictRecordId));
        }

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

        const result = await response.json() as any;
        return { ...result, records: this._parseRecords(result.records ?? []) };
    }

    async commitSync(
        deviceId: number,
        sessionId: number
    ): Promise<{
        createRemoteCount: number;
        updateRemoteCount: number;
        skippedCount: number;
        createLocalCount: number;
        updateLocalCount: number;
        deleteLocalCount: number;
        linkCount: number;
        unlinkCount: number;
        renameCount: number;
        conflictCount: number;
        updateTimestampCount: number;
        errorCount: number;
        committedAt: Date;
    }> {
        const response: any = await this._post(`/devices/${deviceId}/sync/${sessionId}/commit`, {});
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
        deleteLocalCount: number;
        linkCount: number;
        unlinkCount: number;
        renameCount: number;
        conflictCount: number;
        updateTimestampCount: number;
        errorCount: number;
    }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/complete`, {});
    }

    async createPendingActions(deviceId: number, sessionId: number): Promise<{ records: SyncRecordItem[]; counts: SyncActionCounts }> {
        const response = await this._post<{ records: unknown[]; counts: SyncActionCounts }>(`/devices/${deviceId}/sync/${sessionId}/pending-actions`, {});
        return { records: this._parseRecords(response.records), counts: response.counts };
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
                songId: number;
                checksum: string;
                checksumAlgorithm: string;
                localModifiedAt: string;
            }>;
            potentialUpdates: Array<{
                path: string;
                songId: number;
                checksum: string;
                checksumAlgorithm: string;
                localModifiedAt: string;
                lastSyncedAt: string;
            }>;
        }
    ): Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }> {
        const response = await this._post<{ records?: unknown[]; counts: SyncActionCounts }>(`/devices/${deviceId}/sync/${sessionId}/resolve-conflicts`, request);
        return { records: this._parseRecords(response.records ?? []), counts: response.counts };
    }

    async chooseConflicts(
        deviceId: number,
        sessionId: number,
        request: { downloadRecordIds: number[] }
    ): Promise<{
        records: SyncRecordItem[];
        counts: SyncActionCounts;
    }> {
        const response = await this._post<{ records?: unknown[]; counts: SyncActionCounts }>(`/devices/${deviceId}/sync/${sessionId}/conflict-choices`, request);
        return { records: this._parseRecords(response.records ?? []), counts: response.counts };
    }

    async downloadSong(songId: number, destinationPath: string): Promise<void> {
        const response = await fetch(`${this._serverUrl}/songs/${songId}/download`, {
            method: 'GET',
            headers: this._headers(),
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(`API error ${response.status} on download: ${text}`);
        }

        const fs = require('fs');
        fs.writeFileSync(destinationPath, Buffer.from(await response.arrayBuffer()));
    }

    async reportSyncError(
        deviceId: number,
        sessionId: number,
        request: { filePath: string; errorMessage: string; songId?: number | null; recordId?: number | null }
    ): Promise<{ counts: SyncActionCounts }> {
        return this._post(`/devices/${deviceId}/sync/${sessionId}/error`, request);
    }
}
