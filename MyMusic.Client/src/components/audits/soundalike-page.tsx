import {Button, Card, Group, Stack, Text, Modal, Divider, List, ThemeIcon, Badge, Tooltip, useComputedColorScheme} from "@mantine/core";
import {IconCheck, IconTrash} from '@tabler/icons-react';
import {useGetSoundalikeDuplicates, useUpdateSoundalikeSelection} from "../../client/audits.ts";
import {useResolveSoundalikes} from "../../hooks/useResolveSoundalikes.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useCallback, useEffect, useState} from "react";
import type {SoundalikeDuplicateGroup} from "../../model/soundalikeDuplicateGroup.ts";
import type {GetSoundalikeDuplicatesResponse} from "../../model/getSoundalikeDuplicatesResponse.ts";
import type {SoundalikeSongItem} from "../../model/soundalikeSongItem.ts";
import {SecondaryAction} from "../../model/secondaryAction.ts";
import {notifications} from "@mantine/notifications";
import SoundalikeToolbar from "./soundalike-toolbar.tsx";
import SongArtwork from "../common/fields/song-artwork";
import ExplicitLabel from "../common/explicit-label.tsx";
import {formatFileSize} from "../../utils/format-file-size.ts";
import {formatRelativeDate} from "../../utils/format-relative-date.ts";

type SongAction = typeof SecondaryAction.Delete | typeof SecondaryAction.Merge | typeof SecondaryAction.Keep;

interface GroupSelection {
    primaryId: number | null;
    actions: Map<number, SongAction>;
}

interface SoundalikePageProps {
    onToolbarChange: (toolbar: React.ReactNode) => void;
}

function groupSelectionFromServer(group: SoundalikeDuplicateGroup): GroupSelection | undefined {
    if (group.primarySongId == null) return undefined;

    const actions = new Map<number, SongAction>();
    if (group.secondaryActions) {
        for (const [k, v] of Object.entries(group.secondaryActions)) {
            actions.set(Number(k), v as SongAction);
        }
    }

    return {
        primaryId: group.primarySongId,
        actions
    };
}

function toSelectionRequest(selection: GroupSelection) {
    const secondaryActions: Record<string, SongAction> = {};
    for (const [k, v] of selection.actions.entries()) {
        secondaryActions[k] = v;
    }
    return {
        primarySongId: selection.primaryId,
        secondaryActions
    };
}

export default function SoundalikePage({onToolbarChange}: SoundalikePageProps) {
    const soundalikesQuery = useGetSoundalikeDuplicates();
    const resolveMutation = useResolveSoundalikes();
    const selectionMutation = useUpdateSoundalikeSelection();
    const [selectedGroups, setSelectedGroups] = useState<Map<number, GroupSelection>>(new Map());
    const [confirmModalOpen, setConfirmModalOpen] = useState(false);

    const soundalikesResponse = useQueryData(
        soundalikesQuery,
        "Failed to fetch soundalike groups"
    );

    const groups: SoundalikeDuplicateGroup[] = (soundalikesResponse as { data: GetSoundalikeDuplicatesResponse } | null)?.data?.groups ?? [];

    useEffect(() => {
        const serverGroups = (soundalikesResponse as { data: GetSoundalikeDuplicatesResponse } | null)?.data?.groups;
        if (!serverGroups || serverGroups.length === 0) return;

        const initialMap = new Map<number, GroupSelection>();
        for (const group of serverGroups) {
            const selection = groupSelectionFromServer(group);
            if (selection) {
                initialMap.set(group.nonConformityId, selection);
            }
        }
        setSelectedGroups(initialMap);
    }, [soundalikesResponse]);

    const persistSelection = useCallback((nonConformityId: number, selection: GroupSelection) => {
        selectionMutation.mutate({
            nonConformityId,
            data: toSelectionRequest(selection)
        });
    }, [selectionMutation]);

    const handleSelectPrimary = (nonConformityId: number, songId: number) => {
        setSelectedGroups(prev => {
            const newMap = new Map(prev);
            let newSelection: GroupSelection;
            const existing = newMap.get(nonConformityId);
            if (existing) {
                if (existing.primaryId === songId) {
                    newMap.delete(nonConformityId);
                    persistSelection(nonConformityId, {primaryId: null, actions: new Map()});
                    return newMap;
                }
                const newActions = new Map(existing.actions);
                newActions.delete(songId);
                newActions.set(existing.primaryId!, SecondaryAction.Delete);
                newSelection = {
                    primaryId: songId,
                    actions: newActions
                };
            } else {
                const otherSongs = groups
                    .find((g: SoundalikeDuplicateGroup) => g.nonConformityId === nonConformityId)
                    ?.songs.filter((s) => s.id !== songId) ?? [];
                const actions = new Map<number, SongAction>();
                for (const s of otherSongs) {
                    actions.set(s.id, SecondaryAction.Delete);
                }
                newSelection = {
                    primaryId: songId,
                    actions
                };
            }
            newMap.set(nonConformityId, newSelection);
            persistSelection(nonConformityId, newSelection);
            return newMap;
        });
    };

    const handleSetAction = (nonConformityId: number, songId: number, action: SongAction | null) => {
        setSelectedGroups(prev => {
            const newMap = new Map(prev);
            const existing = newMap.get(nonConformityId);
            if (existing) {
                const newActions = new Map(existing.actions);
                if (action) {
                    newActions.set(songId, action);
                } else {
                    newActions.delete(songId);
                }
                const newSelection: GroupSelection = {
                    ...existing,
                    actions: newActions
                };
                newMap.set(nonConformityId, newSelection);
                persistSelection(nonConformityId, newSelection);
            }
            return newMap;
        });
    };

    const handleResolve = async () => {
        setConfirmModalOpen(false);

        const resolutions = Array.from(selectedGroups.entries())
            .filter(([, selection]) => selection.primaryId != null)
            .map(([nonConformityId, selection]) => ({
            nonConformityId,
            primarySongId: selection.primaryId!,
            secondaryActions: Array.from(selection.actions.entries()).map(([songId, action]) => ({
                songId,
                action
            }))
        }));

        await resolveMutation.mutateAsync(
            { resolutions },
            {
                onSuccess: () => {
                    notifications.show({
                        title: "Success",
                        message: `Resolved ${resolutions.length} duplicate group(s)`,
                        color: "green"
                    });
                    setSelectedGroups(new Map());
                    soundalikesQuery.refetch();
                },
                onError: (error: unknown) => {
                    notifications.show({
                        title: "Error",
                        message: `Failed to resolve duplicates: ${error}`,
                        color: "red"
                    });
                }
            }
        );
    };

    const getMergedMetadataPreview = (primarySong: SoundalikeSongItem, secondarySongs: SoundalikeSongItem[]) => {
        const changes: string[] = [];

        if (!primarySong.year && secondarySongs.some(s => s.year)) {
            const year = secondarySongs.find(s => s.year)?.year;
            if (year) changes.push(`Year: ${year}`);
        }

        if (!primarySong.hasLyrics && secondarySongs.some(s => s.hasLyrics)) {
            changes.push("Lyrics");
        }

        if (!primarySong.cover && secondarySongs.some(s => s.cover)) {
            changes.push("Artwork");
        }

        if (!primarySong.bitrate && secondarySongs.some(s => s.bitrate)) {
            const bitrate = secondarySongs.find(s => s.bitrate)?.bitrate;
            if (bitrate) changes.push(`Bitrate: ${bitrate} kbps`);
        }

        const newGenres = secondarySongs.flatMap(s => s.genres)
            .filter(g => !primarySong.genres.some(pg => pg.id === g.id));
        if (newGenres.length > 0) {
            changes.push(`Genres: ${newGenres.map(g => g.name).join(', ')}`);
        }

        const newArtists = secondarySongs.flatMap(s => s.artists)
            .filter(a => !primarySong.artists.some(pa => pa.id === a.id));
        if (newArtists.length > 0) {
            changes.push(`Artists: ${newArtists.map(a => a.name).join(', ')}`);
        }

        return changes;
    };

    const getTotalSongsToDelete = () => {
        return Array.from(selectedGroups.values()).reduce((sum, sel) => {
            return sum + Array.from(sel.actions.values())
                .filter(a => a === SecondaryAction.Delete || a === SecondaryAction.Merge)
                .length;
        }, 0);
    };

    const getTotalSongsToMerge = () => {
        return Array.from(selectedGroups.values()).reduce((sum, sel) => {
            return sum + Array.from(sel.actions.values())
                .filter(a => a === SecondaryAction.Merge)
                .length;
        }, 0);
    };

    const readyToResolve = selectedGroups.size > 0;

    useEffect(() => {
        onToolbarChange(
            <SoundalikeToolbar
                selectedGroupsCount={selectedGroups.size}
                readyToResolve={readyToResolve}
                onRemoveDuplicates={() => setConfirmModalOpen(true)}
            />
        );
    }, [onToolbarChange, selectedGroups.size, readyToResolve]);

    return (
        <div style={{height: '100%', display: 'flex', flexDirection: 'column'}}>
            <Text c="dimmed" mb="md">
                Click a song to mark it as primary. Then choose an action for each remaining song: delete, merge metadata then delete, or keep.
            </Text>

            <div style={{flex: 1, overflow: 'auto'}}>
                <Stack gap="md">
                    {groups.map((group: SoundalikeDuplicateGroup) => (
                        <SoundalikeGroupCard
                            key={group.nonConformityId}
                            group={group}
                            selection={selectedGroups.get(group.nonConformityId)}
                            onSelectPrimary={handleSelectPrimary}
                            onSetAction={handleSetAction}
                        />
                    ))}

                    {groups.length === 0 && (
                        <Text c="dimmed" ta="center" mt="xl">
                            No duplicate songs detected
                        </Text>
                    )}
                </Stack>
            </div>

            <Modal
                opened={confirmModalOpen}
                onClose={() => setConfirmModalOpen(false)}
                title="Confirm Resolution"
                size="lg"
            >
                <Stack>
                    <Text>
                        You are about to process <strong>{selectedGroups.size} group(s)</strong>:
                    </Text>
                    <Group gap="md">
                        <Text size="sm"><strong>{getTotalSongsToDelete()}</strong> song(s) will be deleted</Text>
                        {getTotalSongsToMerge() > 0 && (
                            <Text size="sm"><strong>{getTotalSongsToMerge()}</strong> song(s) will have metadata merged first</Text>
                        )}
                    </Group>

                    <Divider my="sm" />

                    {Array.from(selectedGroups.entries()).map(([groupId, selection]) => {
                        const group = groups.find(g => g.nonConformityId === groupId);
                        if (!group) return null;

                        const primarySong = group.songs.find(s => s.id === selection.primaryId);
                        if (!primarySong) return null;

                        const mergeSongs = group.songs.filter(s => selection.actions.get(s.id) === SecondaryAction.Merge);
                        const deleteSongs = group.songs.filter(s => selection.actions.get(s.id) === SecondaryAction.Delete);
                        const keepSongs = group.songs.filter(s => selection.actions.get(s.id) === SecondaryAction.Keep);

                        const changes = mergeSongs.length > 0 ? getMergedMetadataPreview(primarySong, mergeSongs) : [];

                        return (
                            <Card key={groupId} withBorder padding="sm">
                                <Text fw={600} mb="xs">{primarySong.title}</Text>
                                {changes.length > 0 ? (
                                    <List size="sm" spacing={2} mb="xs">
                                        {changes.map((change) => (
                                            <List.Item key={change} icon={
                                                <ThemeIcon color="blue" size="sm" radius="xl">
                                                    <IconCheck size={12} />
                                                </ThemeIcon>
                                            }>
                                                {change}
                                            </List.Item>
                                        ))}
                                    </List>
                                ) : null}
                                {deleteSongs.length > 0 && (
                                    <Text size="sm" c="dimmed">
                                        Deleting: {deleteSongs.map(s => s.title).join(', ')}
                                    </Text>
                                )}
                                {mergeSongs.length > 0 && (
                                    <Text size="sm" c="dimmed">
                                        Merging then deleting: {mergeSongs.map(s => s.title).join(', ')}
                                    </Text>
                                )}
                                {keepSongs.length > 0 && (
                                    <Text size="sm" c="dimmed">
                                        Keeping: {keepSongs.map(s => s.title).join(', ')}
                                    </Text>
                                )}
                            </Card>
                        );
                    })}

                    <Divider my="sm" />

                    <Group justify="flex-end">
                        <Button variant="default" onClick={() => setConfirmModalOpen(false)}>
                            Cancel
                        </Button>
                        <Button
                            color="red"
                            leftSection={<IconTrash size={16}/>}
                            onClick={handleResolve}
                            loading={resolveMutation.isPending}
                        >
                            Resolve {selectedGroups.size} Group(s)
                        </Button>
                    </Group>
                </Stack>
            </Modal>
        </div>
    );
}

interface SoundalikeGroupCardProps {
    group: SoundalikeDuplicateGroup;
    selection?: GroupSelection;
    onSelectPrimary: (nonConformityId: number, songId: number) => void;
    onSetAction: (nonConformityId: number, songId: number, action: SongAction | null) => void;
}

function SoundalikeGroupCard({group, selection, onSelectPrimary, onSetAction}: SoundalikeGroupCardProps) {
    const colorScheme = useComputedColorScheme('light');
    const matchPercentage = Math.round(group.matchScore * 100);

    const primaryBg = colorScheme === 'dark' ? 'var(--mantine-color-blue-8)' : 'var(--mantine-color-blue-1)';
    const primaryBorder = colorScheme === 'dark' ? 'var(--mantine-color-blue-4)' : 'var(--mantine-color-blue-6)';
    const primaryText = colorScheme === 'dark' ? 'var(--mantine-color-blue-0)' : undefined;
    const keepBadgeVariant = colorScheme === 'dark' ? 'white' : 'light';

    const actionBadgeProps: Record<Exclude<SongAction, typeof SecondaryAction.Keep>, { color: string; label: string }> = {
        [SecondaryAction.Delete]: { color: 'red', label: 'Delete' },
        [SecondaryAction.Merge]: { color: 'orange', label: 'Merge' },
    };

    return (
        <Card shadow="sm" padding="lg" radius="md" withBorder>
            <Group justify="space-between" mb="md">
                <Text fw={500}>Match Score: {matchPercentage}%</Text>
                <Text size="sm" c="dimmed">
                    {group.songs.length} songs
                </Text>
            </Group>

            <Stack gap="sm">
                {group.songs.map((song) => {
                    const isPrimary = selection?.primaryId === song.id;
                    const songAction = selection?.actions.get(song.id);
                    const hasSelection = !!selection;

                    return (
                        <Card
                            key={song.id}
                            padding="sm"
                            withBorder
                            style={{
                                cursor: 'pointer',
                                backgroundColor: isPrimary ? primaryBg : undefined,
                                borderColor: isPrimary ? primaryBorder : undefined
                            }}
                            onClick={() => onSelectPrimary(group.nonConformityId, song.id)}
                        >
                            <Group justify="space-between" wrap="nowrap">
                                <Group style={{minWidth: 0, flex: 1}}>
                                    <Tooltip label={`${song.coverWidth ?? '-'} × ${song.coverHeight ?? '-'}`} openDelay={500}>
                                        <SongArtwork id={song.cover} size={40} />
                                    </Tooltip>
                                    <div style={{minWidth: 0, flex: 1}}>
                                        <ExplicitLabel visible={song.isExplicit}>
                                            <Text fw={isPrimary ? 600 : 400} c={isPrimary ? primaryText : undefined} lineClamp={1} style={{textDecoration: songAction ? 'line-through' : undefined}}>
                                                {song.title}
                                            </Text>
                                        </ExplicitLabel>
                                        <Text size="sm" c={isPrimary ? primaryText : "dimmed"} lineClamp={1} style={{textDecoration: songAction ? 'line-through' : undefined}}>
                                            {song.artists.map(a => a.name).join(', ')} • {song.album?.name ?? 'Unknown Album'}
                                        </Text>
                                        <Tooltip label={song.createdAt ? new Date(song.createdAt).toLocaleString() : undefined} disabled={!song.createdAt} openDelay={500}>
                                            <Text size="xs" c={isPrimary ? primaryText : "dimmed"} lineClamp={1} style={{textDecoration: songAction ? 'line-through' : undefined}}>
                                                {[
                                                    song.duration,
                                                    song.size ? formatFileSize(song.size) : null,
                                                    song.bitrate ? `${song.bitrate} kbps` : null,
                                                    song.genres.length ? song.genres.map(g => g.name).join(', ') : null,
                                                    song.createdAt ? formatRelativeDate(song.createdAt) : null,
                                                ].filter(Boolean).join(' \u2022 ')}
                                            </Text>
                                        </Tooltip>
                                    </div>
                                </Group>
                                <Group gap="xs" wrap="nowrap">
                                    {isPrimary && <Badge color="blue" variant={keepBadgeVariant}>Keep</Badge>}
                                    {!isPrimary && hasSelection && songAction && (
                                        <>
                                            {(['Delete', 'Merge'] as const).map(action => {
                                                const props = actionBadgeProps[action];
                                                const isActive = songAction === action;
                                                return (
                                                    <Badge
                                                        key={action}
                                                        color={props.color}
                                                        variant={isActive ? keepBadgeVariant : 'outline'}
                                                        style={{cursor: 'pointer', opacity: isActive ? 1 : 0.5}}
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            onSetAction(group.nonConformityId, song.id, isActive ? null : action);
                                                        }}
                                                    >
                                                        {props.label}
                                                    </Badge>
                                                );
                                            })}
                                        </>
                                    )}
                                    {!isPrimary && hasSelection && !songAction && (
                                        <>
                                            {(['Delete', 'Merge'] as const).map(action => {
                                                const props = actionBadgeProps[action];
                                                return (
                                                    <Badge
                                                        key={action}
                                                        color={props.color}
                                                        variant="outline"
                                                        style={{cursor: 'pointer', opacity: 0.5}}
                                                        onClick={(e) => {
                                                            e.stopPropagation();
                                                            onSetAction(group.nonConformityId, song.id, action);
                                                        }}
                                                    >
                                                        {props.label}
                                                    </Badge>
                                                );
                                            })}
                                        </>
                                    )}
                                    {song.year && <Text size="sm" c={isPrimary ? primaryText : "dimmed"}>{song.year}</Text>}
                                    {song.hasLyrics && <Badge variant="light" color="grape">Lyrics</Badge>}
                                </Group>
                            </Group>
                        </Card>
                    );
                })}
            </Stack>
        </Card>
    );
}
