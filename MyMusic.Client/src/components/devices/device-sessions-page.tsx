import {useParams, Link} from "@tanstack/react-router";
import {useEffect} from "react";
import {useTranslation} from "react-i18next";
import {Anchor, Breadcrumbs, Text} from "@mantine/core";
import {useGetDevice} from "../../client/devices.ts";
import {useGetDevicesDeviceIdSessions, useDeleteDevicesDeviceIdSessionsSessionId} from "../../client/device-sync-sessions.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useDeviceSessionsSchema} from "./useDeviceSessionsSchema.tsx";
import {modals} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import type {SyncSessionItem} from "../../model";

export default function DeviceSessionsPage() {
    const {t} = useTranslation(["devices", "common"]);
    const {deviceId} = useParams({from: '/devices/$deviceId/sessions/'});
    const deviceIdNum = parseInt(deviceId, 10);
    
    const deviceQuery = useGetDevice(deviceIdNum, {});
    const deviceResponse = useQueryData(deviceQuery, t("devices:sessionsPage.fetchDeviceFailed"));
    const device = deviceResponse?.data?.device;
    
    const sessionsQuery = useGetDevicesDeviceIdSessions(deviceIdNum, {});
    const sessionsResponse = useQueryData(sessionsQuery, t("devices:sessionsPage.fetchFailed"));
    
    const deleteSession = useDeleteDevicesDeviceIdSessionsSessionId();
    const sessionsSchema = useDeviceSessionsSchema(deviceIdNum);
    
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
                        {deviceId: deviceIdNum, sessionId: session.id},
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
    
    const deviceName = device?.name ?? t("devices:sessionsPage.deviceFallback", {id: deviceId});
    
    const breadcrumbItems = [
        {title: t("common:nav.devices"), href: '/devices', isLast: false},
        {title: deviceName, href: `/devices/${deviceId}/sessions`, isLast: false},
        {title: t("devices:sessionsPage.sessions"), href: `/devices/${deviceId}/sessions`, isLast: true},
    ];
    
    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <Breadcrumbs mb="md">
                {breadcrumbItems.map((item) => (
                    item.isLast ? (
                        <Text key="current" fw={500}>{item.title}</Text>
                    ) : (
                        <Anchor key={item.href} component={Link} to={item.href}>
                            {item.title}
                        </Anchor>
                    )
                ))}
            </Breadcrumbs>
            
            <div style={{flex: 1}}>
                <Collection
                    key={`device-sessions-${deviceId}`}
                    stateKey={`device-sessions-${deviceId}`}
                    items={sessions}
                    schema={schemaWithDelete}
                    initialView="table"
                />
            </div>
        </div>
    );
}
