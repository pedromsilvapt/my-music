import {Text} from "@mantine/core";
import {modals} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import {useEffect} from "react";
import {useTranslation} from "react-i18next";
import {useDeleteDevicesDeviceIdSessionsSessionId, useGetDevicesDeviceIdSessions} from "../../client/device-sync-sessions.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {SyncSessionItem} from "../../model";
import Collection from "../common/collection/collection.tsx";
import {useDeviceSessionsSchema} from "./useDeviceSessionsSchema.tsx";

interface DeviceSessionsCollectionProps {
    deviceId: number;
    /** Grows with its sessions instead of filling its parent. */
    autoHeight?: boolean;
}

/**
 * The sync sessions of a device, which can be opened and deleted.
 */
export default function DeviceSessionsCollection({deviceId, autoHeight}: DeviceSessionsCollectionProps) {
    const {t} = useTranslation(["devices", "common"]);

    const sessionsQuery = useGetDevicesDeviceIdSessions(deviceId, {});
    const sessionsResponse = useQueryData(sessionsQuery, t("devices:sessionsPage.fetchFailed"));

    const deleteSession = useDeleteDevicesDeviceIdSessionsSessionId();
    const sessionsSchema = useDeviceSessionsSchema(deviceId);

    const refetch = sessionsQuery.refetch;

    useEffect(() => {
        refetch();
    }, [refetch]);

    const sessions = sessionsResponse?.data?.sessions ?? [];

    const handleDelete = (selectedSessions: SyncSessionItem[]) => {
        modals.openConfirmModal({
            title: t("devices:sessionsPage.deleteTitle"),
            children: (
                <Text size="sm">
                    {selectedSessions.length === 1
                        ? t("devices:sessionsPage.deleteConfirmSingle", {id: selectedSessions[0]!.id})
                        : t("devices:sessionsPage.deleteConfirmPlural", {count: selectedSessions.length})}
                </Text>
            ),
            labels: {confirm: t("common:actions.delete"), cancel: t("common:actions.cancel")},
            confirmProps: {color: 'red'},
            onConfirm: () => {
                selectedSessions.forEach(session => {
                    deleteSession.mutate(
                        {deviceId, sessionId: session.id},
                        {
                            onSuccess: () => {
                                notifications.show({
                                    title: t("devices:sessionsPage.deletedTitle"),
                                    message: t("devices:sessionsPage.deletedMessage", {id: session.id}),
                                    color: 'green',
                                });
                                refetch();
                            },
                            onError: (error) => {
                                notifications.show({
                                    title: t("common:status.error"),
                                    message: t("devices:sessionsPage.deleteFailed", {id: session.id}),
                                    color: 'red',
                                });
                                console.error('Failed to delete session:', error);
                            }
                        }
                    );
                });
            },
        });
    };

    // Override the schema actions to include deviceId
    const schemaWithDelete = {
        ...sessionsSchema,
        actions: () => [
            {group: t("devices:schema.manageGroup")},
            {
                name: "delete",
                renderIcon: () => <span>🗑️</span>,
                renderLabel: () => t("common:actions.delete"),
                onClick: handleDelete,
            }
        ]
    };

    return (
        <Collection
            key={`device-sessions-${deviceId}`}
            stateKey={`device-sessions-${deviceId}`}
            items={sessions}
            schema={schemaWithDelete}
            isFetching={sessionsQuery.isFetching}
            initialView="table"
            autoHeight={autoHeight}
        />
    );
}
