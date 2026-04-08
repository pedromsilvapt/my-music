import {ActionIcon, Button, Group, Modal, ScrollArea, SegmentedControl, Stack, Text, TextInput} from "@mantine/core";
import {useQueryClient} from "@tanstack/react-query";
import {useState} from "react";
import {IconPlus, IconX} from "@tabler/icons-react";
import {useListPlaylists, useManagePlaylistSongs} from "../../client/playlists.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
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
    const playlistsQuery = useListPlaylists();
    const playlistsResponse = useQueryData(playlistsQuery, "Failed to fetch playlists") ?? {data: {playlists: []}};

    const playlists = playlistsResponse?.data?.playlists ?? [];

    const queryClient = useQueryClient();
    const [selections, setSelections] = useState<Map<number, PlaylistSelection>>(new Map());
    const [newPlaylists, setNewPlaylists] = useState<NewPlaylistEntry[]>([]);
    const [newPlaylistName, setNewPlaylistName] = useState("");

    const manageSongs = useManagePlaylistSongs({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: ['api', 'playlists']});
                setSelections(new Map());
                setNewPlaylists([]);
                setNewPlaylistName("");
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

    const handleNewPlaylistSelectionChange = (id: string, value: string) => {
        setNewPlaylists(prev => prev.map(p =>
            p.id === id ? {...p, selection: value as PlaylistSelection} : p
        ));
    };

    const handleAddNewPlaylist = () => {
        const trimmed = newPlaylistName.trim();
        if (!trimmed) return;

        setNewPlaylists(prev => [...prev, {
            id: crypto.randomUUID(),
            name: trimmed,
            selection: "add" as PlaylistSelection,
        }]);
        setNewPlaylistName("");
    };

    const handleRemoveNewPlaylist = (id: string) => {
        setNewPlaylists(prev => prev.filter(p => p.id !== id));
    };

    const handleUpdateNewPlaylistName = (id: string, name: string) => {
        setNewPlaylists(prev => prev.map(p =>
            p.id === id ? {...p, name} : p
        ));
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

        const newPlaylistNames = newPlaylists
            .filter(p => p.selection === "add" && p.name.trim())
            .map(p => p.name.trim());

        if (playlistActions.length > 0 || newPlaylistNames.length > 0) {
            manageSongs.mutate({
                data: {
                    songIds,
                    playlists: playlistActions,
                    newPlaylists: newPlaylistNames.length > 0 ? newPlaylistNames : undefined,
                }
            });
        } else {
            onClose();
        }
    };

    const handleCancel = () => {
        setSelections(new Map());
        setNewPlaylists([]);
        setNewPlaylistName("");
        onClose();
    };

    return (
        <Modal opened={opened} onClose={handleCancel} size="lg" title="Manage Playlists" centered zIndex={ZINDEX_MODAL}>
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

                        {newPlaylists.length > 0 && playlists.length > 0 && (
                            <Divider/>
                        )}

                        {newPlaylists.map(entry => (
                            <NewPlaylistRow
                                key={entry.id}
                                entry={entry}
                                onNameChange={(name) => handleUpdateNewPlaylistName(entry.id, name)}
                                onSelectionChange={(value) => handleNewPlaylistSelectionChange(entry.id, value)}
                                onRemove={() => handleRemoveNewPlaylist(entry.id)}
                            />
                        ))}
                    </Stack>
                </ScrollArea>

                <Group justify="space-between" align="center">
                    <Group gap="xs">
                        <TextInput
                            placeholder="New playlist name"
                            value={newPlaylistName}
                            onChange={(e) => setNewPlaylistName(e.target.value)}
                            onKeyDown={(e) => {
                                if (e.key === 'Enter') handleAddNewPlaylist();
                            }}
                            size="xs"
                            w={200}
                        />
                        <ActionIcon
                            variant="subtle"
                            onClick={handleAddNewPlaylist}
                            disabled={!newPlaylistName.trim()}
                        >
                            <IconPlus size={16}/>
                        </ActionIcon>
                    </Group>
                    <Group>
                        <Button variant="default" onClick={handleCancel}>
                            Cancel
                        </Button>
                        <Button onClick={handleApply} loading={manageSongs.isPending}>
                            Apply
                        </Button>
                    </Group>
                </Group>
            </Stack>
        </Modal>
    );
}

function Divider() {
    return <div style={{borderTop: '1px solid var(--mantine-color-default-border)', margin: '4px 0'}}/>;
}

interface NewPlaylistEntry {
    id: string;
    name: string;
    selection: PlaylistSelection;
}

interface NewPlaylistRowProps {
    entry: NewPlaylistEntry;
    onNameChange: (name: string) => void;
    onSelectionChange: (value: PlaylistSelection) => void;
    onRemove: () => void;
}

function NewPlaylistRow({entry, onNameChange, onSelectionChange, onRemove}: NewPlaylistRowProps) {
    return (
        <Group justify="space-between" wrap="nowrap">
            <TextInput
                value={entry.name}
                onChange={(e) => onNameChange(e.target.value)}
                size="xs"
                style={{flex: 1, minWidth: 0}}
                styles={{input: {fontWeight: 500}}}
            />
            <Group gap="xs" wrap="nowrap">
                <SegmentedControl
                    value={entry.selection}
                    onChange={(v) => onSelectionChange(v as PlaylistSelection)}
                    data={[
                        {label: <Text inherit c="gray">None</Text>, value: 'none'},
                        {label: <Text inherit c={entry.selection === 'add' ? 'green' : 'gray'}>Add</Text>, value: 'add'},
                    ]}
                    size="xs"
                />
                <ActionIcon variant="subtle" color="red" onClick={onRemove} size="sm">
                    <IconX size={14}/>
                </ActionIcon>
            </Group>
        </Group>
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
