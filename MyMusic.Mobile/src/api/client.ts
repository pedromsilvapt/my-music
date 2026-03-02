import {ZodError, ZodSchema} from 'zod';
import {getServerUrl, getUserId, getUserName} from '../services/configService';
import type {ApiError, ProblemDetails} from './types';

async function getAuthHeaders(): Promise<Record<string, string>> {
    const headers: Record<string, string> = {
        'Content-Type': 'application/json',
    };

    try {
        const userId = getUserId();
        if (userId) {
            headers['X-MyMusic-UserId'] = userId.toString();
        }

        const userName = getUserName();
        if (userName) {
            headers['X-MyMusic-UserName'] = userName;
        }
    } catch (error) {
        console.error('Failed to get auth headers:', error);
    }

    return headers;
}

export async function apiRequest<T>(
    endpoint: string,
    options: {
        method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
        body?: unknown;
        schema: ZodSchema<T>;
        skipAuth?: boolean;
    }
): Promise<T> {
    const serverUrl = getServerUrl();
    const url = `${serverUrl}${endpoint}`;
    const headers = options.skipAuth
        ? {'Content-Type': 'application/json'}
        : await getAuthHeaders();

    const response = await fetch(url, {
        method: options.method ?? 'GET',
        headers,
        body: options.body ? JSON.stringify(options.body) : undefined,
    });

    if (!response.ok) {
        let detail = `Request failed with status ${response.status}`;

        try {
            const errorData = await response.json();
            const parsed = errorData as ProblemDetails;
            detail = parsed.detail || parsed.title || detail;
        } catch {
            // Response wasn't JSON
        }

        const error: ApiError = {
            status: response.status,
            message: detail,
        };
        throw error;
    }

    if (response.status === 204) {
        return undefined as T;
    }

    try {
        const data = await response.json();
        return options.schema.parse(data);
    } catch (error) {
        if (error instanceof ZodError) {
            console.error('Response validation failed:', error.issues);
            throw {
                status: response.status,
                message: 'Response validation failed',
            } as ApiError;
        }
        throw error;
    }
}

export async function apiMultipartRequest<T>(
    endpoint: string,
    formData: FormData,
    schema: ZodSchema<T>
): Promise<T> {
    const headers = await getAuthHeaders();
    delete headers['Content-Type'];

    const serverUrl = getServerUrl();
    const url = `${serverUrl}${endpoint}`;
    const response = await fetch(url, {
        method: 'POST',
        headers,
        body: formData,
    });

    if (!response.ok) {
        let detail = `Request failed with status ${response.status}`;

        try {
            const errorData = await response.json();
            const parsed = errorData as ProblemDetails;
            detail = parsed.detail || parsed.title || detail;
        } catch {
            // Response wasn't JSON
        }

        const error: ApiError = {
            status: response.status,
            message: detail,
        };
        throw error;
    }

    const data = await response.json();
    return schema.parse(data);
}

export async function downloadSong(songId: number): Promise<Blob> {
    const headers = await getAuthHeaders();
    const serverUrl = getServerUrl();
    const response = await fetch(`${serverUrl}/songs/${songId}/download`, {
        headers,
    });

    if (!response.ok) {
        throw new Error(`Failed to download song: ${response.status}`);
    }

    return response.blob();
}

export async function testConnection(url?: string): Promise<{ success: boolean; message: string }> {
    try {
        const testUrl = url || getServerUrl();
        const headers = await getAuthHeaders();
        const response = await fetch(`${testUrl}/ping`, {headers});

        if (response.ok) {
            return {success: true, message: 'Connected successfully!'};
        }

        return {success: false, message: `Server returned ${response.status}`};
    } catch (error: any) {
        if (error.message?.includes('Network request failed')) {
            return {success: false, message: 'Cannot connect to server. Check URL and network.'};
        }
        return {success: false, message: error.message || 'Connection failed'};
    }
}