import type { DeviceOptions, ISyncApiClient } from './types';

type DeviceOptionsApi = Pick<ISyncApiClient, 'updateDevice'>;

/** The options of a device as the server stores them. */
export interface ServerDeviceOptions {
    icon: string | null;
    color: string | null;
    namingTemplate: string | null;
    importOnPurchase: boolean;
}

/**
 * Saves the local device options to the server device when they differ, returning whether it did.
 * The server replaces every option on update, so the color, which the app has no setting for, is sent
 * back unchanged.
 */
export async function saveDeviceOptions (
    api: DeviceOptionsApi,
    deviceId: number,
    server: ServerDeviceOptions,
    local: DeviceOptions
): Promise<boolean> {
    const upToDate = server.icon === local.icon
        && server.namingTemplate === local.namingTemplate
        && server.importOnPurchase === local.importOnPurchase;

    if (upToDate) {
        return false;
    }

    await api.updateDevice(deviceId, {
        icon: local.icon ?? undefined,
        color: server.color ?? undefined,
        namingTemplate: local.namingTemplate ?? undefined,
        importOnPurchase: local.importOnPurchase,
    });

    return true;
}
