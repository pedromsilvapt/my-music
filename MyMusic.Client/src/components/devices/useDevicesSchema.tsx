import {Text} from "@mantine/core";
import {modals} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import {IconTrash} from "@tabler/icons-react";
import {useCallback, useMemo} from "react";
import {useDeleteApiDevicesDeviceId} from "../../client/devices.ts";
import type {ListDeviceItem} from "../../model";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import DeviceBadge from "./device-badge.tsx";

export function useDevicesSchema() {
    const deleteDevice = useDeleteApiDevicesDeviceId();

    const handleDelete = useCallback((devices: ListDeviceItem[]) => {
        modals.openConfirmModal({
            title: 'Delete Device',
            children: (
                <Text size="sm">
                    Are you sure you want to delete {devices.length === 1
                    ? `"${devices[0]!.name}"`
                    : `${devices.length} devices`}?
                    This will also remove all associated songs and sync history. This action cannot be undone.
                </Text>
            ),
            labels: {confirm: 'Delete', cancel: 'Cancel'},
            confirmProps: {color: 'red'},
            onConfirm: () => {
                for (const device of devices) {
                    deleteDevice.mutate(
                        {deviceId: device.id},
                        {
                            onSuccess: () => {
                                notifications.show({
                                    title: 'Device Deleted',
                                    message: `Device "${device.name}" has been deleted.`,
                                    color: 'green',
                                });
                            },
                            onError: (error) => {
                                notifications.show({
                                    title: 'Error',
                                    message: `Failed to delete device "${device.name}"`,
                                    color: 'red',
                                });
                                console.error('Failed to delete device:', error);
                            }
                        }
                    );
                }
            },
        });
    }, [deleteDevice]);

    return useMemo(() => ({
        key: row => row.id,
        searchVector: device => device.name,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'icon',
                displayName: '',
                render: row => <DeviceBadge name={row.name} icon={row.icon} color={row.color} showTooltip={false}/>,
                width: 120,
            },
            {
                name: 'name',
                displayName: 'Name',
                render: row => <Text fw={500}>{row.name}</Text>,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'songCount',
                displayName: 'Songs',
                render: row => <Text>{row.songCount}</Text>,
                width: 80,
                align: 'center',
                sortable: true,
            },
            {
                name: 'namingTemplate',
                displayName: 'Naming Template',
                render: row => <Text size="sm" c="dimmed">{row.namingTemplate ?? 'Default'}</Text>,
                width: '2fr',
            },
        ],

        actions: () => {
            return [
                {group: "Manage"},
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => "Delete",
                    onClick: handleDelete,
                }
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: () => <DeviceBadge
            name=""
            icon="IconDevices"
        />,
        renderListTitle: (row) => <Text fw={500}>{row.name}</Text>,
        renderListSubTitle: (row) => <Text c="gray">{row.songCount} songs</Text>,
    }) as CollectionSchema<ListDeviceItem>, [handleDelete]);
}
