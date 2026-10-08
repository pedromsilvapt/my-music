import {Ionicons} from '@expo/vector-icons';

export interface DeviceTypeInfo {
    id: string;
    label: string;
    icon: keyof typeof Ionicons.glyphMap;
}

export const DEVICE_TYPES: DeviceTypeInfo[] = [
    {id: 'IconDeviceMobile', label: 'Smartphone', icon: 'phone-portrait'},
    {id: 'IconDeviceTablet', label: 'Tablet', icon: 'tablet-portrait'},
    {id: 'IconDeviceLaptop', label: 'Laptop', icon: 'laptop'},
    {id: 'IconDevicesPc', label: 'Desktop', icon: 'desktop'},
    {id: 'IconUsb', label: 'USB Drive', icon: 'flash'},
    {id: 'IconDeviceMp3', label: 'MP3 Player', icon: 'musical-notes'},
];

// Ids saved by older versions of the app
const LEGACY_DEVICE_TYPE_IDS: Record<string, string> = {
    IconUSBDrive: 'IconUsb',
    IconMP3Player: 'IconDeviceMp3',
};

const findDeviceType = (id: string | undefined) =>
    DEVICE_TYPES.find(d => d.id === (id !== undefined ? LEGACY_DEVICE_TYPE_IDS[id] ?? id : id));

export const DEFAULT_DEVICE_TYPE = DEVICE_TYPES[0]; // Smartphone

export function getDeviceIcon(id: string | undefined): string {
    return findDeviceType(id)?.icon ?? 'phone-portrait';
}

export function getDeviceOutlineIcon(id: string | undefined): string {
    const icon = getDeviceIcon(id);
    return `${icon}-outline`;
}

export function getDeviceTypeIcon(label: string): string {
    const device = DEVICE_TYPES.find(d => d.label === label || d.id === `Icon${label}`);
    return device?.icon ?? 'phone-portrait';
}

export function getDeviceTypeById(id: string): DeviceTypeInfo | undefined {
    return findDeviceType(id);
}

export function getDeviceTypeIdByLabel(label: string): string {
    const device = DEVICE_TYPES.find(d => d.label === label);
    return device?.id ?? DEFAULT_DEVICE_TYPE.id;
}