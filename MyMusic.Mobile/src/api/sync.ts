import * as SecureStore from 'expo-secure-store';
import {getServerUrl} from '../services/configService';
import {downloadFile} from '../../modules/repo-files';
import {apiMultipartRequest, apiRequest} from './client';
import type {AcknowledgeActionRequest, PruneSessionsRequest, ReportSyncErrorRequest, SyncCheckRequest, SyncConflictChoicesRequest, SyncResolveConflictsRequest, SyncStartRequest} from './types';
import {
    AcknowledgeActionResponseSchema,
    CreatePendingActionsResponseSchema,
    DeleteSessionResponseSchema,
    ListSyncRecordsResponseSchema,
    ListSyncSessionsResponseSchema,
    PruneSessionsResponseSchema,
    ReportSyncErrorResponseSchema,
    SyncCheckResponseSchema,
    SyncCommitResponseSchema,
    SyncCompleteResponseSchema,
    SyncDeduplicatePrepareResponseSchema,
    SyncConflictChoicesResponseSchema,
    SyncResolveConflictsResponseSchema,
    SyncStartResponseSchema,
    SyncUploadResponseSchema,
} from './types';

async function getAuthHeaders(): Promise<Record<string, string>> {
    const headers: Record<string, string> = {};

    try {
        const userId = await SecureStore.getItemAsync('userId');
        if (userId) {
            headers['X-MyMusic-UserId'] = userId;
        }

        const userName = await SecureStore.getItemAsync('userName');
        if (userName) {
            headers['X-MyMusic-UserName'] = userName;
        }
    } catch (error) {
        console.error('Failed to get auth headers:', error);
    }

    return headers;
}

export async function startSync(deviceId: number, request: SyncStartRequest) {
    return apiRequest(`/devices/${deviceId}/sync/start`, {
        method: 'POST',
        body: request,
        schema: SyncStartResponseSchema,
    });
}

export async function prepareDeduplicate(deviceId: number, sessionId: number) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/deduplicate/prepare`, {
        method: 'POST',
        schema: SyncDeduplicatePrepareResponseSchema,
    });
}

export async function checkSync(deviceId: number, sessionId: number, request: SyncCheckRequest) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/check`, {
        method: 'POST',
        body: request,
        schema: SyncCheckResponseSchema,
    });
}

export async function completeSync(deviceId: number, sessionId: number) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/complete`, {
        method: 'POST',
        schema: SyncCompleteResponseSchema,
    });
}

export async function commitSync(deviceId: number, sessionId: number) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/commit`, {
        method: 'POST',
        schema: SyncCommitResponseSchema,
    });
}

export async function uploadFile(
    deviceId: number,
    sessionId: number,
    file: { uri: string; name: string },
    path: string,
    modifiedAt: string,
    createdAt: string,
    resolvesConflictRecordId?: number
) {
    const formData = new FormData();
    formData.append('file', file as any);
    formData.append('path', path);
    formData.append('modifiedAt', modifiedAt);
    formData.append('createdAt', createdAt);
    if (resolvesConflictRecordId != null) {
        formData.append('resolvesConflictRecordId', String(resolvesConflictRecordId));
    }

    return apiMultipartRequest(`/devices/${deviceId}/sync/${sessionId}/upload`, formData, SyncUploadResponseSchema);
}

export async function createPendingActions(deviceId: number, sessionId: number) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/pending-actions`, {
        method: 'POST',
        schema: CreatePendingActionsResponseSchema,
    });
}

export async function acknowledgeAction(deviceId: number, sessionId: number, request: AcknowledgeActionRequest) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/acknowledge`, {
        method: 'POST',
        body: request,
        schema: AcknowledgeActionResponseSchema,
    });
}

export async function getSessions(deviceId: number, count: number = 10) {
    return apiRequest(`/devices/${deviceId}/sessions?count=${count}`, {
        schema: ListSyncSessionsResponseSchema,
    });
}

export async function getSessionRecords(
    deviceId: number,
    sessionId: number,
    actions?: string,
    source?: string,
    limit?: number,
    offset?: number | null,
    sort?: string
) {
    let url = `/devices/${deviceId}/sessions/${sessionId}/records`;
    const params = new URLSearchParams();
    if (actions) params.set('actions', actions);
    if (source) params.set('source', source);
    if (limit !== undefined) params.set('limit', limit.toString());
    if (offset !== undefined && offset !== null) params.set('offset', offset.toString());
    if (sort) params.set('sort', sort);
    const queryString = params.toString();
    if (queryString) url += `?${queryString}`;

    return apiRequest(url, {
        schema: ListSyncRecordsResponseSchema,
    });
}

/**
 * Downloads the file of a song to the given local path, replacing any file already there.
 * The download is streamed to disk natively: React Native's Blob cannot be read as bytes
 * (it has no `arrayBuffer()`), and a song should not be held in memory anyway. It goes through the
 * repo-files module because expo-file-system cannot create files in shared storage.
 */
export async function downloadSong(songId: number, destinationPath: string): Promise<void> {
    const headers = await getAuthHeaders();
    const url = `${getServerUrl()}/songs/${songId}/download`;

    try {
        await downloadFile(url, headers, destinationPath);
    } catch (e) {
        throw new Error(`Failed to download song ${songId}: ${e instanceof Error ? e.message : String(e)}`);
    }
}

export async function deleteSession(deviceId: number, sessionId: number) {
    return apiRequest(`/devices/${deviceId}/sessions/${sessionId}`, {
        method: 'DELETE',
        schema: DeleteSessionResponseSchema,
    });
}

export async function pruneSessions(deviceId: number, request: PruneSessionsRequest) {
    return apiRequest(`/devices/${deviceId}/sessions/prune`, {
        method: 'POST',
        body: request,
        schema: PruneSessionsResponseSchema,
    });
}

export async function resolveConflicts(deviceId: number, sessionId: number, request: SyncResolveConflictsRequest) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/resolve-conflicts`, {
        method: 'POST',
        body: request,
        schema: SyncResolveConflictsResponseSchema,
    });
}

export async function chooseConflicts(deviceId: number, sessionId: number, request: SyncConflictChoicesRequest) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/conflict-choices`, {
        method: 'POST',
        body: request,
        schema: SyncConflictChoicesResponseSchema,
    });
}

export async function reportSyncError(deviceId: number, sessionId: number, request: ReportSyncErrorRequest) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/error`, {
        method: 'POST',
        body: request,
        schema: ReportSyncErrorResponseSchema,
    });
}