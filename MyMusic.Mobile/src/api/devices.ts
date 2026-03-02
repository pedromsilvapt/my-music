import {z} from 'zod';
import {apiRequest} from './client';
import type {CreateDeviceRequest, UpdateDeviceRequest} from './types';
import {
    CreateDeviceResponseSchema,
    GetDeviceResponseSchema,
    ListDevicesResponseSchema,
    UpdateDeviceResponseSchema,
} from './types';

export async function createDevice(request: CreateDeviceRequest) {
    return apiRequest('/devices', {
        method: 'POST',
        body: request,
        schema: CreateDeviceResponseSchema,
    });
}

export async function updateDevice(deviceId: number, request: UpdateDeviceRequest) {
    return apiRequest(`/devices/${deviceId}`, {
        method: 'PUT',
        body: request,
        schema: UpdateDeviceResponseSchema,
    });
}

export async function getDevices() {
    return apiRequest('/devices', {
        schema: ListDevicesResponseSchema,
    });
}

export async function getDevice(deviceId: number) {
    return apiRequest(`/devices/${deviceId}`, {
        schema: GetDeviceResponseSchema,
    });
}

export async function deleteDevice(deviceId: number) {
    return apiRequest(`/devices/${deviceId}`, {
        method: 'DELETE',
        schema: z.nullable(z.any()),
    });
}