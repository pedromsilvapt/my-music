import {createDevice, getDevices, updateDevice} from '../api/devices';
import {getDeviceTypeById, getDeviceTypeIdByLabel} from '../constants/deviceIcons';
import {getDeviceIcon, getDeviceName, getImportOnPurchase, getNamingTemplate, setDeviceId} from './configService';
import {saveDeviceOptions} from './sync/device-options';
import type {DeviceOptions} from './sync/types';

/**
 * The device options configured in the app, in the shape the server stores them.
 */
export function getLocalDeviceOptions(): DeviceOptions {
    const icon = getDeviceIcon();

    return {
        icon: getDeviceTypeById(icon)?.id ?? getDeviceTypeIdByLabel(icon),
        namingTemplate: getNamingTemplate() || null,
        importOnPurchase: getImportOnPurchase(),
    };
}

/**
 * Saves the device configured in the app to the server: registers it when no device has its name,
 * otherwise saves the options that differ. Stores and returns the device id.
 */
export async function saveDeviceConfig(): Promise<number> {
    const name = getDeviceName();
    const options = getLocalDeviceOptions();

    const {devices} = await getDevices();
    const existingDevice = devices.find(d => d.name === name);

    if (existingDevice) {
        await saveDeviceOptions({updateDevice}, existingDevice.id, existingDevice, options);
        await setDeviceId(existingDevice.id);
        return existingDevice.id;
    }

    const {device} = await createDevice({
        name,
        icon: options.icon ?? undefined,
        namingTemplate: options.namingTemplate ?? undefined,
        importOnPurchase: options.importOnPurchase,
    });
    await setDeviceId(device.id);
    return device.id;
}
