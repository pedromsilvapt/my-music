import {Button, Group, Modal, ScrollArea, SegmentedControl, Stack, Text} from "@mantine/core";
import {useQueryClient} from "@tanstack/react-query";
import {useState} from "react";
import {useListPlaylists, useManagePlaylistSongs} from "../../client/playlists.ts";
import type {ListPlaylistItem, PlaylistAction, PlaylistSongAction} from "../../model";

type PlaylistSelection = "none" | "add" | "remove";

interface ManagePlaylistsDialogProps {
    opened: boolean;
    onClose: () => void;
    songIds: number[];
    onSuccess?: () => void;
}

export default function ManagePlaylistsDialog({
                                                  opened,
                                                  onClose,
                                                  songIds,
                                                  onSuccess
                                              }: ManagePlaylistsDialogProps) {
    const {data: playlistsData} = useListPlaylists();
    const playlists = playlistsData?.data?.playlists ?? [];

    const queryClient = useQueryClient();
    const [selections, setSelections] = useState<Map<number, PlaylistSelection>>(new Map());

    const manageSongs = useManagePlaylistSongs({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: ['api', 'playlists']});
                setSelections(new Map());
                onClose();
                onSuccess?.();
            }
        }
    });

    const handleSelectionChange = (playlistId: number, value: string) => {
        setSelections(prev => {
            const newMap = new Map(prev);
            const valueEnum = value as PlaylistSelection;
            if (valueEnum === "none") {
                newMap.delete(playlistId);
            } else {
                newMap.set(playlistId, valueEnum);
            }
            return newMap;
        });
    };

    const handleApply = () => {
        const playlistActions: PlaylistSongAction[] = [];

        selections.forEach((selection, playlistId) => {
            if (selection === "add") {
                playlistActions.push({
                    playlistId,
                    action: "Add" as PlaylistAction
                });
            } else if (selection === "remove") {
                playlistActions.push({
                    playlistId,
                    action: "Remove" as PlaylistAction
                });
            }
        });

        if (playlistActions.length > 0) {
            manageSongs.mutate({
                data: {
                    songIds,
                    playlists: playlistActions
                }
            });
        } else {
            onClose();
        }
    };

    const handleCancel = () => {
        setSelections(new Map());
        onClose();
    };

    return (
        <Modal opened={opened} onClose={handleCancel} size="lg" title="Manage Playlists" centered>
            <Stack>
                <Text size="sm" c="dimmed">
                    Managing {songIds.length} song{songIds.length !== 1 ? "s" : ""}
                </Text>

                <ScrollArea h={300}>
                    <Stack gap="sm">
                        {playlists.map(playlist => (
                            <PlaylistRow
                                key={playlist.id}
                                playlist={playlist}
                                value={selections.get(playlist.id) ?? "none"}
                                onChange={(value) => handleSelectionChange(playlist.id, value)}
                            />
                        ))}
                    </Stack>
                </ScrollArea>

                <Group justify="space-between">
                    <Button variant="default" onClick={handleCancel}>
                        Cancel
                    </Button>
                    <Button onClick={handleApply} loading={manageSongs.isPending}>
                        Apply
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}

interface PlaylistRowProps {
    playlist: ListPlaylistItem;
    value: PlaylistSelection;
    onChange: (value: PlaylistSelection) => void;
}

function PlaylistRow({playlist, value, onChange}: PlaylistRowProps) {
    return (
        <Group justify="space-between">
            <Text fw={500}>{playlist.name}</Text>
            <SegmentedControl
                value={value}
                onChange={(v) => onChange(v as PlaylistSelection)}
                data={[
                    {label: <Text inherit c="gray">None</Text>, value: 'none'},
                    {label: <Text inherit c={value === 'add' ? 'green' : 'gray'}>Add</Text>, value: 'add'},
                    {label: <Text inherit c={value === 'remove' ? 'red' : 'gray'}>Remove</Text>, value: 'remove'},
                ]}
                size="xs"
            />
        </Group>
    );
}
