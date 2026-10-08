import {getDevices, updateDevice} from '../api/devices';
import type {ListDeviceItem} from '../api/types';
import {getDeviceName, setDeviceIcon, setDeviceId} from './configService';

/** The options of the server device the app has a setting for. */
export interface DeviceOptions {
    icon: string | null;
    namingTemplate: string | null;
    importOnPurchase: boolean;
}

/** The options of a device as loaded from the server, with what is needed to save them back. */
export interface LoadedDeviceOptions extends DeviceOptions {
    deviceId: number;
    /** The app has no setting for the color: it is sent back unchanged when the options are saved. */
    color: string | null;
}

/** The server has no device with the configured name. Devices are created in the web app. */
export class DeviceNotFoundError extends Error {
    constructor(deviceName: string) {
        super(`Device '${deviceName}' not found. Create it in the web app (Devices > New device)`);
        this.name = 'DeviceNotFoundError';
    }
}

/**
 * Finds the server device that has the configured device name, and stores its id and icon. The app never
 * creates devices: a missing one is an error.
 */
export async function findDevice(): Promise<ListDeviceItem> {
    const name = getDeviceName();

    const {devices} = await getDevices();
    const device = devices.find(d => d.name === name);

    if (!device) {
        throw new DeviceNotFoundError(name);
    }

    await setDeviceId(device.id);
    await storeDeviceIcon(device.icon);

    return device;
}

/**
 * Loads the options of the configured device from the server, their owner.
 */
export async function loadDeviceOptions(): Promise<LoadedDeviceOptions> {
    const device = await findDevice();

    return {
        deviceId: device.id,
        icon: device.icon,
        color: device.color,
        namingTemplate: device.namingTemplate,
        importOnPurchase: device.importOnPurchase,
    };
}

/**
 * Saves the options of a device to the server. The server replaces every option on update, so the color
 * the options were loaded with is sent back; the name is left out, which keeps it.
 */
export async function saveDeviceOptions(loaded: LoadedDeviceOptions, options: DeviceOptions): Promise<void> {
    await updateDevice(loaded.deviceId, {
        icon: options.icon ?? undefined,
        color: loaded.color ?? undefined,
        namingTemplate: options.namingTemplate ?? undefined,
        importOnPurchase: options.importOnPurchase,
    });

    await storeDeviceIcon(options.icon);
}

/** The home and settings screens show the icon without asking the server. */
async function storeDeviceIcon(icon: string | null): Promise<void> {
    if (icon) {
        await setDeviceIcon(icon);
    }
}
