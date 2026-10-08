/** The icons a device can be given, with the translation key of their label (`devices:icons.<key>`). */
export const DEVICE_ICONS = [
    {icon: "IconDeviceMobile", key: "smartphone"},
    {icon: "IconDeviceTablet", key: "tablet"},
    {icon: "IconDeviceLaptop", key: "laptop"},
    {icon: "IconDevicesPc", key: "desktop"},
    {icon: "IconUsb", key: "usb"},
    {icon: "IconDeviceMp3", key: "mp3Player"},
] as const;

/** The icon shown for a device that has none. */
export const DEFAULT_DEVICE_ICON = "IconDeviceDesktop";
