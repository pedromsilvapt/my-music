import * as SecureStore from 'expo-secure-store';
import {getServerUrl} from '../services/configService';
import {apiMultipartRequest, apiRequest} from './client';
import type {AcknowledgeActionRequest, PruneSessionsRequest, SyncCheckRequest, SyncRecordsRequest, SyncStartRequest} from './types';
import {
    AcknowledgeActionResponseSchema,
    DeleteSessionResponseSchema,
    GetPendingActionsResponseSchema,
    ListSyncRecordsResponseSchema,
    ListSyncSessionsResponseSchema,
    PruneSessionsResponseSchema,
    SyncCheckResponseSchema,
    SyncCompleteResponseSchema,
    SyncRecordsResponseSchema,
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

export async function checkSync(deviceId: number, request: SyncCheckRequest) {
    return apiRequest(`/devices/${deviceId}/sync/check`, {
        method: 'POST',
        body: request,
        schema: SyncCheckResponseSchema,
    });
}

export async function recordChunk(deviceId: number, sessionId: number, request: SyncRecordsRequest) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/records`, {
        method: 'POST',
        body: request,
        schema: SyncRecordsResponseSchema,
    });
}

export async function completeSync(deviceId: number, sessionId: number) {
    return apiRequest(`/devices/${deviceId}/sync/${sessionId}/complete`, {
        method: 'POST',
        schema: SyncCompleteResponseSchema,
    });
}

export async function uploadFile(
    deviceId: number,
    file: { uri: string; name: string; type: string },
    path: string,
    modifiedAt: string,
    createdAt: string
) {
    const formData = new FormData();
    formData.append('file', file as any);
    formData.append('path', path);
    formData.append('modifiedAt', modifiedAt);
    formData.append('createdAt', createdAt);

    return apiMultipartRequest(`/devices/${deviceId}/sync/upload`, formData, SyncUploadResponseSchema);
}

export async function getPendingActions(deviceId: number) {
    return apiRequest(`/devices/${deviceId}/sync/pending-actions`, {
        schema: GetPendingActionsResponseSchema,
    });
}

export async function acknowledgeAction(deviceId: number, request: AcknowledgeActionRequest) {
    return apiRequest(`/devices/${deviceId}/sync/acknowledge`, {
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
    source?: string
) {
    let url = `/devices/${deviceId}/sessions/${sessionId}/records`;
    const params = new URLSearchParams();
    if (actions) params.set('actions', actions);
    if (source) params.set('source', source);
    const queryString = params.toString();
    if (queryString) url += `?${queryString}`;

    return apiRequest(url, {
        schema: ListSyncRecordsResponseSchema,
    });
}

export async function downloadSong(songId: number): Promise<Blob> {
    const headers = await getAuthHeaders();
    const response = await fetch(`${getServerUrl()}/songs/${songId}/download`, {
        headers,
    });

    if (!response.ok) {
        throw new Error(`Failed to download song: ${response.status}`);
    }

    return response.blob();
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