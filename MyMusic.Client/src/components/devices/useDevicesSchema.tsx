import {Code, Text, Anchor} from "@mantine/core";
import {modals} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import {IconTrash, IconHistory} from "@tabler/icons-react";
import {useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {Link} from "@tanstack/react-router";
import {useDeleteDevicesDeviceId} from "../../client/devices.ts";
import type {ListDeviceItem} from "../../model";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import TablerIcon from "../common/tabler-icon.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";
import {TEXT_COLOR} from "../../utils/colors.ts";

export function useDevicesSchema() {
    const {t} = useTranslation(["devices", "common"]);
    const deleteDevice = useDeleteDevicesDeviceId();
    const {data: filterMetadata} = useFilterMetadata('devices');

    const handleDelete = useCallback((devices: ListDeviceItem[]) => {
        modals.openConfirmModal({
            title: t("devices:schema.deleteTitle"),
            children: (
                <Text size="sm">
                    {devices.length === 1
                        ? t("devices:schema.deleteConfirmSingle", {name: devices[0]!.name})
                        : t("devices:schema.deleteConfirmPlural", {count: devices.length})}
                </Text>
            ),
            labels: {confirm: t("common:actions.delete"), cancel: t("common:actions.cancel")},
            confirmProps: {color: 'red'},
            onConfirm: () => {
                for (const device of devices) {
                    deleteDevice.mutate(
                        {deviceId: device.id},
                        {
                            onSuccess: () => {
                                notifications.show({
                                    title: t("devices:schema.deletedTitle"),
                                    message: t("devices:schema.deletedMessage", {name: device.name}),
                                    color: 'green',
                                });
                            },
                            onError: (error) => {
                                notifications.show({
                                    title: t("common:status.error"),
                                    message: t("devices:schema.deleteFailed", {name: device.name}),
                                    color: 'red',
                                });
                                console.error('Failed to delete device:', error);
                            }
                        }
                    );
                }
            },
        });
    }, [deleteDevice, t]);

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
                displayName: t("devices:schema.columns.name"),
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
                displayName: t("devices:schema.columns.songs"),
                render: row => <Text>{row.songCount}</Text>,
                width: 80,
                align: 'center',
                sortable: true,
            },
            {
                name: 'namingTemplate',
                displayName: t("devices:schema.columns.namingTemplate"),
                render: row => <Code>{row.namingTemplate ?? t("devices:schema.default")}</Code>,
                width: '2fr',
            },
            {
                name: 'lastSyncAt',
                displayName: t("devices:schema.columns.lastSync"),
                render: row => <Text c="dimmed">{row.lastSyncAt ? new Date(row.lastSyncAt).toLocaleString() : t("devices:schema.never")}</Text>,
                width: 180,
                sortable: true,
                getValue: row => row.lastSyncAt ?? null,
            },
        ],

        actions: () => {
            return [
                {group: t("devices:schema.manageGroup")},
                {
                    name: "view-sessions",
                    renderIcon: () => <IconHistory/>,
                    renderLabel: () => t("devices:schema.viewSessions"),
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
                    renderLabel: () => t("common:actions.delete"),
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
        renderListSubTitle: (row) => <Text c="gray">{t("common:count.songs", {count: row.songCount})}</Text>,
    }) as CollectionSchema<ListDeviceItem>, [handleDelete, filterMetadata, fetchFilterValues, t]);
}
