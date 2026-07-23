import {Badge, Box, Button, Collapse, Group, Modal, ScrollArea, SegmentedControl, Stack, Text} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useState} from "react";
import {IconChevronDown, IconChevronUp} from "@tabler/icons-react";
import {useGetDevices} from "../../client/devices.ts";
import {useListSongs, useUpdateSongDevices} from "../../client/songs.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListDeviceItem, ListSongItem} from "../../model";
import DeviceBadge from "./device-badge.tsx";
import ManageSongItem from "../common/manage-song-item.tsx";

type DeviceSelection = "none" | "add" | "remove";

interface ManageDevicesDialogProps {
    opened: boolean;
    onClose: () => void;
    songIds: number[];
    onSuccess?: () => void;
}

export default function ManageDevicesDialog({
                                               opened,
                                               onClose,
                                               songIds,
                                               onSuccess
                                           }: ManageDevicesDialogProps) {
    const devicesQuery = useGetDevices({ includeSongs: true }, {query: {enabled: opened}});
    const devicesResponse = useQueryData(devicesQuery, "Failed to fetch devices") ?? {data: {devices: []}};
    const devices = devicesResponse.data.devices ?? [];

    const songsQuery = useListSongs(
        songIds.length > 0 ? {filter: `id in [${songIds.join(',')}]`} : undefined,
        {query: {enabled: opened && songIds.length > 0}}
    );
    const songsResponse = useQueryData(songsQuery, "Failed to fetch songs") ?? {data: {songs: []}};
    const managedSongs = songsResponse?.data?.songs ?? [];

    const queryClient = useQueryClient();
    const [selections, setSelections] = useState<Map<number, DeviceSelection>>(new Map());
    const [expandedDevices, setExpandedDevices] = useState<Set<number>>(new Set());

    const updateDevices = useUpdateSongDevices({
        mutation: {
            onSuccess: () => {
                songIds.forEach(id => {
                    queryClient.invalidateQueries({queryKey: ['api', 'songs', id]});
                });
                queryClient.invalidateQueries({queryKey: ['api', 'devices']});
                setSelections(new Map());
                setExpandedDevices(new Set());
                onClose();
                onSuccess?.();
            },
            onError: (error: unknown) => {
                const errorResponse = error as { response?: { data?: { detail?: string } }; message?: string } | null;
                const errorMessage = errorResponse?.response?.data?.detail
                    ?? errorResponse?.message
                    ?? 'Failed to update devices. Please try again.';
                notifications.show({
                    title: 'Error',
                    message: errorMessage,
                    color: 'red',
                });
                console.error('Failed to update devices:', error);
            }
        }
    });

    const handleSelectionChange = (deviceId: number, value: string) => {
        setSelections(prev => {
            const newMap = new Map(prev);
            const valueEnum = value as DeviceSelection;
            if (valueEnum === "none") {
                newMap.delete(deviceId);
            } else {
                newMap.set(deviceId, valueEnum);
            }
            return newMap;
        });
    };

    const handleToggleExpand = (deviceId: number) => {
        setExpandedDevices(prev => {
            const next = new Set(prev);
            if (next.has(deviceId)) {
                next.delete(deviceId);
            } else {
                next.add(deviceId);
            }
            return next;
        });
    };

    const handleApply = () => {
        const updates: { deviceId: number; include: boolean }[] = [];

        selections.forEach((selection, deviceId) => {
            if (selection === "add") {
                updates.push({deviceId, include: true});
            } else if (selection === "remove") {
                updates.push({deviceId, include: false});
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
        setSelections(new Map());
        setExpandedDevices(new Set());
        onClose();
    };

    return (
        <Modal opened={opened} onClose={handleCancel} size="lg" title="Manage Devices" centered
               zIndex={ZINDEX_MODAL}>
            <Stack>
                <Text size="sm" c="dimmed">
                    Managing {songIds.length} song{songIds.length !== 1 ? "s" : ""}
                </Text>

                <ScrollArea h={400}>
                    <Stack gap="sm">
                        {devices.map(device => (
                            <DeviceRow
                                key={device.id}
                                device={device}
                                managedSongs={managedSongs}
                                value={selections.get(device.id) ?? "none"}
                                expanded={expandedDevices.has(device.id)}
                                onToggleExpand={() => handleToggleExpand(device.id)}
                                onChange={(value) => handleSelectionChange(device.id, value)}
                            />
                        ))}
                    </Stack>
                </ScrollArea>

                <Group justify="flex-end">
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
    device: ListDeviceItem;
    managedSongs: ListSongItem[];
    value: DeviceSelection;
    expanded: boolean;
    onToggleExpand: () => void;
    onChange: (value: DeviceSelection) => void;
}

function DeviceRow({device, managedSongs, value, expanded, onToggleExpand, onChange}: DeviceRowProps) {
    // We can assume `device.songs` is never null only because in the query above, `includeSongs` is hardcoded to true
    const deviceSongIdSet = new Set(device.songs!.map(s => s.id));
    const deviceSongPathMap = new Map(device.songs!.map(s => [s.id, s.path]));
    const deviceSongSyncActionMap = new Map(device.songs!.map(s => [s.id, s.syncAction]));
    const matchingCount = managedSongs.filter(s => deviceSongIdSet.has(s.id)).length;

    return (
        <Box data-testid={`device-row-${device.id}`}>
            <Group justify="space-between" wrap="nowrap">
                <Group gap="sm" align="center" style={{flex: 1, minWidth: 0}}>
                    <DeviceBadge
                        name={device.name}
                        icon={device.icon}
                        color={device.color}
                        showTooltip={false}
                    />
                </Group>
                <Group gap="xs" wrap="nowrap">
                    <Badge
                        data-testid="device-expand-badge"
                        size="sm"
                        variant="light"
                        color={matchingCount > 0 ? "green" : "gray"}
                        onClick={onToggleExpand}
                        style={{cursor: 'pointer'}}
                        leftSection={
                            expanded ? <IconChevronUp size={12}/> : <IconChevronDown size={12}/>
                        }
                    >
                        {matchingCount}/{managedSongs.length}
                    </Badge>
                    <SegmentedControl
                        value={value}
                        onChange={(v) => onChange(v as DeviceSelection)}
                        data={[
                            {label: <Text inherit c="gray">None</Text>, value: 'none'},
                            {label: <Text inherit c={value === 'add' ? 'green' : 'gray'}>Add</Text>, value: 'add'},
                            {label: <Text inherit c={value === 'remove' ? 'red' : 'gray'}>Remove</Text>, value: 'remove'},
                        ]}
                        size="xs"
                    />
                </Group>
            </Group>
            <Collapse in={expanded}>
                <Stack gap="xs" pl="sm" pt="xs">
                    {managedSongs.map(song => {
                        const isOnDevice = deviceSongIdSet.has(song.id);
                        const path = deviceSongPathMap.get(song.id);
                        const syncAction = deviceSongSyncActionMap.get(song.id);
                        return (
                            <ManageSongItem
                                key={song.id}
                                song={song}
                                isIncluded={isOnDevice}
                                path={isOnDevice ? path : null}
                                syncAction={isOnDevice ? syncAction : null}
                            />
                        );
                    })}
                </Stack>
            </Collapse>
        </Box>
    );
}
