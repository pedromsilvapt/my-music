import {Code, Text, Anchor} from "@mantine/core";
import {modals} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import {IconTrash, IconHistory} from "@tabler/icons-react";
import {useCallback, useMemo} from "react";
import {Link} from "@tanstack/react-router";
import {useDeleteDevicesDeviceId} from "../../client/devices.ts";
import type {ListDeviceItem} from "../../model";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import TablerIcon from "../common/tabler-icon.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";
import {TEXT_COLOR} from "../../utils/colors.ts";

export function useDevicesSchema() {
    const deleteDevice = useDeleteDevicesDeviceId();
    const {data: filterMetadata} = useFilterMetadata('devices');

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

    const fetchFilterValues = useCallback(async (field: string, searchTerm: string) => {
        const params = new URLSearchParams({field, limit: "15"});
        if (searchTerm) params.set("search", searchTerm);
        const response = await fetch(`/api/devices/filter-values?${params}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.values as string[];
    }, []);

    return useMemo(() => ({
        key: row => row.id,
        searchVector: device => device.name,
        filterMetadata,
        fetchFilterValues,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'icon',
                displayName: '',
                render: row => <TablerIcon icon={row.icon} defaultIcon="IconDeviceDesktop" size={20}
                                           color={row.color || 'gray'}/>,
                width: 60,
            },
            {
                name: 'name',
                displayName: 'Name',
                render: row => (
                    <Anchor component={Link} to={`/devices/${row.id}/sessions`} c={TEXT_COLOR}>
                        <Text fw={500}>{row.name}</Text>
                    </Anchor>
                ),
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
                render: row => <Code>{row.namingTemplate ?? 'Default'}</Code>,
                width: '2fr',
            },
            {
                name: 'lastSyncAt',
                displayName: 'Last Sync',
                render: row => <Text c="dimmed">{row.lastSyncAt ? new Date(row.lastSyncAt).toLocaleString() : 'Never'}</Text>,
                width: 180,
                sortable: true,
                getValue: row => row.lastSyncAt ?? null,
            },
        ],

        actions: () => {
            return [
                {group: "Manage"},
                {
                    name: "view-sessions",
                    renderIcon: () => <IconHistory/>,
                    renderLabel: () => "View Sessions",
                    onClick: (devices: ListDeviceItem[]) => {
                        const device = devices[0];
                        if (device) {
                            window.location.href = `/devices/${device.id}/sessions`;
                        }
                    },
                },
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => "Delete",
                    onClick: handleDelete,
                }
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: () => <TablerIcon icon="IconDevices" size={40} color="gray"/>,
        renderListTitle: (row) => (
            <Anchor component={Link} to={`/devices/${row.id}/sessions`} c={TEXT_COLOR}>
                <Text fw={500}>{row.name}</Text>
            </Anchor>
        ),
        renderListSubTitle: (row) => <Text c="gray">{row.songCount} songs</Text>,
    }) as CollectionSchema<ListDeviceItem>, [handleDelete, filterMetadata, fetchFilterValues]);
}
