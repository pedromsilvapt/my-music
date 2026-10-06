import {Badge, Box, Button, Collapse, Group, Modal, ScrollArea, SegmentedControl, Stack, Text} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import {IconChevronDown, IconChevronUp} from "@tabler/icons-react";
import {useGetDevices} from "../../client/devices.ts";
import {getPreviewSongDevicePathsQueryKey, useListSongs, usePreviewSongDevicePaths, useUpdateSongDevices} from "../../client/songs.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {DeviceSongRef, ListDeviceItem, ListSongItem, SongDevicePathItem} from "../../model";
import DeviceBadge from "./device-badge.tsx";
import ManageSongItem from "../common/manage-song-item.tsx";

type DeviceSelection = "none" | "add" | "remove";

/** The paths typed in the dialog, by device and then by song. */
type PathEdits = Map<number, Map<number, string>>;

interface SongPath {
    /** The path of the song on the device: the one it has, the one typed for it before, or the one adding it would give. */
    path: string | null;
    /** Where the device currently holds the file, when the next sync is going to rename it. */
    renamedFrom: string | null;
    /** Whether a path can be typed, given what is selected for the device. */
    editable: boolean;
}

function getSongPath(deviceSong: DeviceSongRef | undefined, selection: DeviceSelection, previewPath: string | undefined): SongPath {
    if (!deviceSong) {
        return {path: previewPath ?? null, renamedFrom: null, editable: selection === "add" && previewPath !== undefined};
    }

    const renamed = !!deviceSong.requestedPath && deviceSong.requestedPath !== deviceSong.path;
    // A song marked for removal only keeps a path when it is added back
    const staysOnDevice = deviceSong.syncAction === "Remove" ? selection === "add" : selection !== "remove";

    return {
        path: deviceSong.requestedPath ?? deviceSong.path,
        renamedFrom: renamed ? deviceSong.path : null,
        editable: staysOnDevice,
    };
}

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
    const {t} = useTranslation(["devices", "common"]);
    const devicesQuery = useGetDevices({ includeSongs: true }, {query: {enabled: opened}});
    const devicesResponse = useQueryData(devicesQuery, t("devices:page.fetchFailed")) ?? {data: {devices: []}};
    const devices = devicesResponse.data.devices ?? [];

    const songsQuery = useListSongs(
        songIds.length > 0 ? {filter: `id in [${songIds.join(',')}]`} : undefined,
        {query: {enabled: opened && songIds.length > 0}}
    );
    const songsResponse = useQueryData(songsQuery, t("devices:manageDialog.fetchSongsFailed")) ?? {data: {songs: []}};
    const managedSongs = songsResponse?.data?.songs ?? [];

    const previewsQuery = usePreviewSongDevicePaths(
        {songIds: songIds.join(',')},
        {query: {enabled: opened && songIds.length > 0}}
    );
    const previewPaths = useMemo(() => {
        const paths = new Map<number, Map<number, string>>();
        const response = previewsQuery.data;
        if (response && response.status < 400) {
            for (const item of response.data.items ?? []) {
                if (!paths.has(item.deviceId)) {
                    paths.set(item.deviceId, new Map());
                }
                paths.get(item.deviceId)!.set(item.songId, item.path);
            }
        }
        return paths;
    }, [previewsQuery.data]);

    const queryClient = useQueryClient();
    const [selections, setSelections] = useState<Map<number, DeviceSelection>>(new Map());
    const [expandedDevices, setExpandedDevices] = useState<Set<number>>(new Set());
    const [pathEdits, setPathEdits] = useState<PathEdits>(new Map());

    const resetState = () => {
        setSelections(new Map());
        setExpandedDevices(new Set());
        setPathEdits(new Map());
    };

    const showUpdateError = (message?: string) => {
        notifications.show({
            title: t("common:status.error"),
            message: message ?? t("devices:manageDialog.updateFailedFallback"),
            color: 'red',
        });
    };

    const updateDevices = useUpdateSongDevices({
        mutation: {
            onSuccess: (response) => {
                // The server rejects the whole update (e.g. a path that is taken): keep the dialog open to fix it
                if (response.status >= 400) {
                    showUpdateError((response.data as { detail?: string } | undefined)?.detail);
                    return;
                }

                songIds.forEach(id => {
                    queryClient.invalidateQueries({queryKey: ['api', 'songs', id]});
                });
                queryClient.invalidateQueries({queryKey: ['api', 'devices']});
                queryClient.invalidateQueries({queryKey: getPreviewSongDevicePathsQueryKey()});
                resetState();
                onClose();
                onSuccess?.();
            },
            onError: (error: unknown) => {
                const errorResponse = error as { response?: { data?: { detail?: string } }; message?: string } | null;
                showUpdateError(errorResponse?.response?.data?.detail ?? errorResponse?.message);
                console.error('Failed to update devices:', error);
            }
        }
    });

    const handlePathChange = (deviceId: number, songId: number, path: string) => {
        setPathEdits(prev => {
            const next = new Map(prev);
            next.set(deviceId, new Map(prev.get(deviceId)).set(songId, path));
            return next;
        });
    };

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

        // Only the paths that were changed, of songs that are (or are being put) on the device
        const paths: SongDevicePathItem[] = [];

        for (const device of devices) {
            const selection = selections.get(device.id) ?? "none";

            pathEdits.get(device.id)?.forEach((typedPath, songId) => {
                const deviceSong = device.songs?.find(s => s.id === songId);
                const songPath = getSongPath(deviceSong, selection, previewPaths.get(device.id)?.get(songId));

                if (songPath.editable && typedPath.trim() !== songPath.path) {
                    paths.push({deviceId: device.id, songId, path: typedPath.trim()});
                }
            });
        }

        if (updates.length > 0 || paths.length > 0) {
            updateDevices.mutate({
                data: {songIds, updates, paths}
            });
        } else {
            resetState();
            onClose();
        }
    };

    const handleCancel = () => {
        resetState();
        onClose();
    };

    return (
        <Modal opened={opened} onClose={handleCancel} size="lg" title={t("devices:manageDialog.title")} centered
               zIndex={ZINDEX_MODAL}>
            <Stack>
                <Text size="sm" c="dimmed">
                    {t("devices:manageDialog.managing", {count: songIds.length})}
                </Text>

                <ScrollArea h={400}>
                    <Stack gap="sm">
                        {devices.map(device => (
                            <DeviceRow
                                key={device.id}
                                device={device}
                                managedSongs={managedSongs}
                                previewPaths={previewPaths.get(device.id)}
                                pathEdits={pathEdits.get(device.id)}
                                onPathChange={(songId, path) => handlePathChange(device.id, songId, path)}
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
                        {t("common:actions.cancel")}
                    </Button>
                    <Button onClick={handleApply} loading={updateDevices.isPending}>
                        {t("common:actions.apply")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}

interface DeviceRowProps {
    device: ListDeviceItem;
    managedSongs: ListSongItem[];
    previewPaths?: Map<number, string>;
    pathEdits?: Map<number, string>;
    onPathChange: (songId: number, path: string) => void;
    value: DeviceSelection;
    expanded: boolean;
    onToggleExpand: () => void;
    onChange: (value: DeviceSelection) => void;
}

function DeviceRow({device, managedSongs, previewPaths, pathEdits, onPathChange, value, expanded, onToggleExpand, onChange}: DeviceRowProps) {
    const {t} = useTranslation(["devices", "common"]);
    // We can assume `device.songs` is never null only because in the query above, `includeSongs` is hardcoded to true
    const deviceSongs = new Map(device.songs!.map(s => [s.id, s]));
    const matchingCount = managedSongs.filter(s => deviceSongs.has(s.id)).length;

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
                            {label: <Text inherit c="gray">{t("common:common.none")}</Text>, value: 'none'},
                            {label: <Text inherit c={value === 'add' ? 'green' : 'gray'}>{t("common:common.add")}</Text>, value: 'add'},
                            {label: <Text inherit c={value === 'remove' ? 'red' : 'gray'}>{t("common:common.remove")}</Text>, value: 'remove'},
                        ]}
                        size="xs"
                    />
                </Group>
            </Group>
            <Collapse in={expanded}>
                <Stack gap="xs" pl="sm" pt="xs">
                    {managedSongs.map(song => {
                        const deviceSong = deviceSongs.get(song.id);
                        const songPath = getSongPath(deviceSong, value, previewPaths?.get(song.id));
                        return (
                            <ManageSongItem
                                key={song.id}
                                song={song}
                                isIncluded={!!deviceSong}
                                path={songPath.editable ? (pathEdits?.get(song.id) ?? songPath.path) : songPath.path}
                                syncAction={deviceSong?.syncAction}
                                onPathChange={songPath.editable ? (path) => onPathChange(song.id, path) : undefined}
                                pathLabel={t("devices:manageDialog.pathLabel")}
                                pathHint={songPath.renamedFrom
                                    ? t("devices:manageDialog.pendingRename", {path: songPath.renamedFrom})
                                    : null}
                            />
                        );
                    })}
                </Stack>
            </Collapse>
        </Box>
    );
}
