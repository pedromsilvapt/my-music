import {useParams, Link} from "@tanstack/react-router";
import {useEffect} from "react";
import {Anchor, Breadcrumbs, Text} from "@mantine/core";
import {useGetDevicesDeviceIdSessions, useDeleteDevicesDeviceIdSessionsSessionId, useGetDevice} from "../../client/devices.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useDeviceSessionsSchema} from "./useDeviceSessionsSchema.tsx";
import {modals} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import type {SyncSessionItem} from "../../model";

export default function DeviceSessionsPage() {
    const {deviceId} = useParams({from: '/devices/$deviceId/sessions/'});
    const deviceIdNum = parseInt(deviceId, 10);
    
    const deviceQuery = useGetDevice(deviceIdNum, {});
    const deviceResponse = useQueryData(deviceQuery, "Failed to fetch device");
    const device = deviceResponse?.data?.device;
    
    const sessionsQuery = useGetDevicesDeviceIdSessions(deviceIdNum, {});
    const sessionsResponse = useQueryData(sessionsQuery, "Failed to fetch device sessions");
    
    const deleteSession = useDeleteDevicesDeviceIdSessionsSessionId();
    const sessionsSchema = useDeviceSessionsSchema(deviceIdNum);
    
    const refetch = sessionsQuery.refetch;
    
    useEffect(() => {
        refetch();
    }, [refetch]);
    
    const sessions = sessionsResponse?.data?.sessions ?? [];
    
    const handleDelete = (selectedSessions: SyncSessionItem[]) => {
        modals.openConfirmModal({
            title: 'Delete Sync Session',
            children: (
                <Text size="sm">
                    Are you sure you want to delete {selectedSessions.length === 1
                    ? `session #${selectedSessions[0]!.id}`
                    : `${selectedSessions.length} sessions`}? 
                    This will also delete all associated sync records. This action cannot be undone.
                </Text>
            ),
            labels: {confirm: 'Delete', cancel: 'Cancel'},
            confirmProps: {color: 'red'},
            onConfirm: () => {
                selectedSessions.forEach(session => {
                    deleteSession.mutate(
                        {deviceId: deviceIdNum, sessionId: session.id},
                        {
                            onSuccess: () => {
                                notifications.show({
                                    title: 'Session Deleted',
                                    message: `Session #${session.id} has been deleted.`,
                                    color: 'green',
                                });
                                refetch();
                            },
                            onError: (error) => {
                                notifications.show({
                                    title: 'Error',
                                    message: `Failed to delete session #${session.id}`,
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
            {group: "Manage"},
            {
                name: "delete",
                renderIcon: () => <span>🗑️</span>,
                renderLabel: () => "Delete",
                onClick: handleDelete,
            }
        ]
    };
    
    const deviceName = device?.name ?? `Device ${deviceId}`;
    
    const breadcrumbItems = [
        {title: 'Devices', href: '/devices', isLast: false},
        {title: deviceName, href: `/devices/${deviceId}/sessions`, isLast: false},
        {title: 'Sessions', href: `/devices/${deviceId}/sessions`, isLast: true},
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
