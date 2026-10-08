import {Anchor, Breadcrumbs, Text} from "@mantine/core";
import {Link} from "@tanstack/react-router";
import {useTranslation} from "react-i18next";

export interface DeviceBreadcrumbItem {
    title: string;
    /** Where the item leads. The last item is the current page, and never a link. */
    href?: string;
}

interface DeviceBreadcrumbsProps {
    deviceId: number | string;
    /** The name of the device, once known. */
    deviceName?: string;
    /** What comes after the device: the pages below its details page. */
    items?: DeviceBreadcrumbItem[];
}

/**
 * The breadcrumbs of the pages of a device: Devices > (the device, leading to its details page) > items.
 */
export default function DeviceBreadcrumbs({deviceId, deviceName, items = []}: DeviceBreadcrumbsProps) {
    const {t} = useTranslation(["devices", "common"]);

    const allItems: DeviceBreadcrumbItem[] = [
        {title: t("common:nav.devices"), href: '/devices'},
        {title: deviceName ?? t("devices:detailPage.deviceFallback", {id: deviceId}), href: `/devices/${deviceId}`},
        ...items,
    ];

    return (
        <Breadcrumbs mb="md" data-testid="device-breadcrumbs">
            {allItems.map((item, index) => (
                index === allItems.length - 1 || !item.href ? (
                    <Text key={index} fw={500} data-testid="device-breadcrumb-current">{item.title}</Text>
                ) : (
                    <Anchor key={index} component={Link} to={item.href} data-testid="device-breadcrumb-link">
                        {item.title}
                    </Anchor>
                )
            ))}
        </Breadcrumbs>
    );
}
