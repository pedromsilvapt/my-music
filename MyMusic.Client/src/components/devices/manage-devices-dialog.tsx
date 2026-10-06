import {Badge, Box, Button, Collapse, Group, Modal, ScrollArea, SegmentedControl, Stack, Text} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import {IconChevronDown, IconChevronUp} from "@tabler/icons-react";
import {useUpdateSongDevices} from "../../client/songs.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {songDevicesQueryKey, useSongDevicesQuery} from "../../hooks/use-song-devices-query.ts";
import type {
    QuerySongDevicesCopy,
    QuerySongDevicesDevice,
    QuerySongDevicesSong,
    SongDeviceCopyItem,
    SongDevicePathItem
} from "../../model";
import DeviceBadge from "./device-badge.tsx";
import ManageSongItem from "../common/manage-song-item.tsx";

type DeviceSelection = "none" | "add" | "remove";

/** The paths typed in the dialog, by device and then by row (see {@link SongRow.key}). */
type PathEdits = Map<number, Map<string, string>>;

/**
 * A song under a device: one row per copy of the song on the device (a song can be at several of its
 * paths), or a single row for a song that is not on it.
 */
interface SongRow {
    key: string;
    song: QuerySongDevicesSong;
    /** The copy the row is for. Undefined for a song that is not on the device. */
    copy?: QuerySongDevicesCopy;
    /** The path of the song on the device: the one it has, the one typed for it before, or the one adding it would give. */
    path: string | null;
    /** Where the device currently holds the file, when the next sync is going to rename it. */
    renamedFrom: string | null;
    /** Whether a path can be typed, given what is selected for the device and the copy. */
    editable: boolean;
    /** Whether the copy is (or stays) waiting to be removed from the device. */
    removing: boolean;
    /** Whether what is selected for the device decides alone what happens to the copy. */
    removalLocked: boolean;
}

function getSongRows(
    device: QuerySongDevicesDevice,
    songs: QuerySongDevicesSong[],
    selection: DeviceSelection,
    toggledCopies: Set<number>,
): SongRow[] {
    const previewPaths = new Map(device.pathPreviews.map(p => [p.songId, p.path]));

    return songs.flatMap((song): SongRow[] => {
        const copies = device.copies.filter(c => c.songId === song.id);

        if (copies.length === 0) {
            const previewPath = previewPaths.get(song.id);
            return [{
                key: `song-${song.id}`,
                song,
                path: previewPath ?? null,
                renamedFrom: null,
                editable: selection === "add" && previewPath !== undefined,
                removing: false,
                removalLocked: false,
            }];
        }

        // Adding a song that is on the device only brings its copies back when none of them is staying
        const restoresAll = selection === "add" && copies.every(c => c.syncAction === "Remove");

        return copies.map(copy => {
            const markedForRemoval = copy.syncAction === "Remove";
            const toggled = toggledCopies.has(copy.songDeviceId);
            const removalLocked = selection === "remove" || (markedForRemoval && restoresAll);
            const removing = selection === "remove"
                || (markedForRemoval ? !(toggled || restoresAll) : toggled);
            const renamed = !!copy.requestedPath && copy.requestedPath !== copy.path;

            return {
                key: `copy-${copy.songDeviceId}`,
                song,
                copy,
                path: copy.requestedPath ?? copy.path,
                renamedFrom: renamed ? copy.path : null,
                editable: !removing,
                removing,
                removalLocked,
            };
        });
    });
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
    const songDevicesQuery = useSongDevicesQuery(songIds, opened);
    const songDevices = useQueryData(songDevicesQuery, t("devices:manageDialog.fetchSongsFailed"));
    const devices = useMemo(() => songDevices?.devices ?? [], [songDevices]);
    const managedSongs = useMemo(() => songDevices?.songs ?? [], [songDevices]);

    const queryClient = useQueryClient();
    const [selections, setSelections] = useState<Map<number, DeviceSelection>>(new Map());
    const [expandedDevices, setExpandedDevices] = useState<Set<number>>(new Set());
    const [pathEdits, setPathEdits] = useState<PathEdits>(new Map());
    // The copies whose remove button was pressed: removed, or restored when they were waiting to be removed
    const [toggledCopies, setToggledCopies] = useState<Set<number>>(new Set());

    const resetState = () => {
        setSelections(new Map());
        setExpandedDevices(new Set());
        setPathEdits(new Map());
        setToggledCopies(new Set());
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
                queryClient.invalidateQueries({queryKey: songDevicesQueryKey});
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

    const handlePathChange = (deviceId: number, rowKey: string, path: string) => {
        setPathEdits(prev => {
            const next = new Map(prev);
            next.set(deviceId, new Map(prev.get(deviceId)).set(rowKey, path));
            return next;
        });
    };

    const handleToggleCopy = (songDeviceId: number) => {
        setToggledCopies(prev => {
            const next = new Set(prev);
            if (next.has(songDeviceId)) {
                next.delete(songDeviceId);
            } else {
                next.add(songDeviceId);
            }
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
        // Only the copies removed or restored one by one: the others follow what is selected for their device
        const copies: SongDeviceCopyItem[] = [];

        for (const device of devices) {
            const selection = selections.get(device.id) ?? "none";

            for (const row of getSongRows(device, managedSongs, selection, toggledCopies)) {
                if (row.copy && !row.removalLocked && toggledCopies.has(row.copy.songDeviceId)) {
                    copies.push({songDeviceId: row.copy.songDeviceId, include: !row.removing});
                }

                const typedPath = pathEdits.get(device.id)?.get(row.key)?.trim();
                if (row.editable && typedPath !== undefined && typedPath !== row.path) {
                    paths.push({
                        deviceId: device.id,
                        songId: row.song.id,
                        songDeviceId: row.copy?.songDeviceId,
                        path: typedPath,
                    });
                }
            }
        }

        if (updates.length > 0 || paths.length > 0 || copies.length > 0) {
            updateDevices.mutate({
                data: {songIds, updates, paths, copies}
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
            <Stack data-testid="manage-devices" data-loading={songDevicesQuery.isFetching ? "true" : "false"}>
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
                                toggledCopies={toggledCopies}
                                onToggleCopy={handleToggleCopy}
                                pathEdits={pathEdits.get(device.id)}
                                onPathChange={(rowKey, path) => handlePathChange(device.id, rowKey, path)}
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
    device: QuerySongDevicesDevice;
    managedSongs: QuerySongDevicesSong[];
    toggledCopies: Set<number>;
    onToggleCopy: (songDeviceId: number) => void;
    pathEdits?: Map<string, string>;
    onPathChange: (rowKey: string, path: string) => void;
    value: DeviceSelection;
    expanded: boolean;
    onToggleExpand: () => void;
    onChange: (value: DeviceSelection) => void;
}

function DeviceRow({device, managedSongs, toggledCopies, onToggleCopy, pathEdits, onPathChange, value, expanded, onToggleExpand, onChange}: DeviceRowProps) {
    const {t} = useTranslation(["devices", "common"]);
    const songIdsOnDevice = new Set(device.copies.map(c => c.songId));
    const matchingCount = managedSongs.filter(s => songIdsOnDevice.has(s.id)).length;
    // The rows are only shown when the device is expanded
    const rows = expanded ? getSongRows(device, managedSongs, value, toggledCopies) : [];

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
                    {rows.map(row => (
                        <ManageSongItem
                            key={row.key}
                            song={row.song}
                            copyId={row.copy?.songDeviceId}
                            isIncluded={!!row.copy}
                            path={row.editable ? (pathEdits?.get(row.key) ?? row.path) : row.path}
                            syncAction={row.copy?.syncAction}
                            onPathChange={row.editable ? (path) => onPathChange(row.key, path) : undefined}
                            pathLabel={t("devices:manageDialog.pathLabel")}
                            pathHint={row.renamedFrom
                                ? t("devices:manageDialog.pendingRename", {path: row.renamedFrom})
                                : null}
                            removal={row.copy && {
                                removing: row.removing,
                                onToggle: () => onToggleCopy(row.copy!.songDeviceId),
                                removeLabel: t("devices:manageDialog.removeCopy"),
                                restoreLabel: t("devices:manageDialog.restoreCopy"),
                                disabled: row.removalLocked,
                            }}
                        />
                    ))}
                </Stack>
            </Collapse>
        </Box>
    );
}
