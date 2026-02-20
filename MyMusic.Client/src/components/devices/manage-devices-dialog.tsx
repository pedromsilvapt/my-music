import {Button, Code, Group, Modal, ScrollArea, Stack, Switch, Text} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {useGetSongDevices, useUpdateSongDevices} from "../../client/songs.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import type {SongDeviceItem} from "../../model";
import DeviceBadge from "./device-badge.tsx";

interface ManageDevicesDialogProps {
    opened: boolean;
    onClose: () => void;
    songIds: number[];
    onSuccess?: () => void;
}

type SwitchState = boolean | 'indeterminate';

export default function ManageDevicesDialog({
                                                opened,
                                                onClose,
                                                songIds,
                                                onSuccess
                                            }: ManageDevicesDialogProps) {
    const {data: devicesData} = useGetSongDevices(songIds[0]!, {query: {enabled: opened && songIds.length > 0}});
    const devices = devicesData?.data?.devices ?? [];

    const queryClient = useQueryClient();

    const [switchStates, setSwitchStates] = useState<Map<number, {
        state: SwitchState;
        hasChanged: boolean
    }>>(new Map());

    useEffect(() => {
        if (devices.length > 0) {
            const initialStates = new Map<number, { state: SwitchState; hasChanged: boolean }>();
            devices.forEach(device => {
                const isOnDevice = device.syncAction === "Download" ||
                    (device.syncAction !== "Remove" && device.path !== null);
                initialStates.set(device.deviceId, {state: isOnDevice, hasChanged: false});
            });
            setSwitchStates(initialStates);
        }
    }, [devices]);

    const updateDevices = useUpdateSongDevices({
        mutation: {
            onSuccess: () => {
                songIds.forEach(id => {
                    queryClient.invalidateQueries({queryKey: ['api', 'songs', id]});
                });
                setSwitchStates(new Map());
                onClose();
                onSuccess?.();
            },
            onError: (error) => {
                notifications.show({
                    title: 'Error',
                    message: 'Failed to update devices. Please try again.',
                    color: 'red',
                });
                console.error('Failed to update devices:', error);
            }
        }
    });

    const handleSwitchChange = (deviceId: number, checked: boolean) => {
        setSwitchStates(prev => {
            const newMap = new Map(prev);
            newMap.set(deviceId, {state: checked, hasChanged: true});
            return newMap;
        });
    };

    const handleApply = () => {
        const updates: { deviceId: number; include: boolean }[] = [];

        switchStates.forEach((value, deviceId) => {
            if (value.hasChanged && typeof value.state === 'boolean') {
                updates.push({deviceId, include: value.state});
            }
        });

        if (updates.length > 0) {
            updateDevices.mutate({
                data: {songIds, updates}
            });
        } else {
            onClose();
        }
    };

    const handleCancel = () => {
        setSwitchStates(new Map());
        onClose();
    };

    const isMultiple = songIds.length > 1;

    return (
        <Modal opened={opened} onClose={handleCancel} size="lg" title="Manage Devices" centered zIndex={ZINDEX_MODAL}>
            <Stack>
                <Text size="sm" c="dimmed">
                    {isMultiple
                        ? `Managing ${songIds.length} songs`
                        : "Select which devices should have this song"}
                </Text>

                <ScrollArea h={300}>
                    <Stack gap="sm">
                        {devices.map(device => (
                            <DeviceRow
                                key={device.deviceId}
                                device={device}
                                switchState={switchStates.get(device.deviceId)?.state ?? false}
                                onChange={(checked) => handleSwitchChange(device.deviceId, checked)}
                            />
                        ))}
                    </Stack>
                </ScrollArea>

                <Group justify="space-between">
                    <Button variant="default" onClick={handleCancel}>
                        Cancel
                    </Button>
                    <Button onClick={handleApply} loading={updateDevices.isPending}>
                        Apply
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}

interface DeviceRowProps {
    device: SongDeviceItem;
    switchState: SwitchState;
    onChange: (checked: boolean) => void;
}

function DeviceRow({device, switchState, onChange}: DeviceRowProps) {
    return (
        <Group justify="space-between" align="center">
            <Group gap="sm" align="center" style={{flex: 1}}>
                <DeviceBadge
                    name={device.deviceName}
                    icon={device.deviceIcon}
                    color={device.deviceColor}
                    syncAction={device.syncAction}
                    showTooltip={false}
                />
                {device.path && (
                    <Code>{device.path}</Code>
                )}
            </Group>
            <Switch
                checked={switchState === true}
                onChange={(e) => onChange(e.currentTarget.checked)}
            />
        </Group>
    );
}
