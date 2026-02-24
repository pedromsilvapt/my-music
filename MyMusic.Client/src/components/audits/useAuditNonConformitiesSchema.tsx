import {Badge, Group, Text} from "@mantine/core";
import {IconCheck, IconEdit, IconTrash, IconX} from "@tabler/icons-react";
import {useMemo} from "react";
import type {ListAuditNonConformitiesItem} from "../../model";
import type {CollectionSchema} from "../common/collection/collection";
import SongAlbum from "../common/fields/song-album";
import SongArtists from "../common/fields/song-artists";
import SongArtwork from "../common/fields/song-artwork";
import SongTitle from "../common/fields/song-title";

export function useAuditNonConformitiesSchema(
    onSetWaiver: (ids: number[], hasWaiver: boolean, reason?: string | null) => void,
    onDelete: (ids: number[]) => void,
    onEditSongs: (songIds: number[]) => void
): CollectionSchema<ListAuditNonConformitiesItem> {
    return useMemo(() => ({
        key: row => row.id,
        searchVector: nc => `${nc.song.title} - ${nc.song.artists.map(a => a.name).join(', ')} - ${nc.song.album.name}`,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: nc => <SongArtwork id={nc.song.cover} size={40}/>,
                width: 52,
            },
            {
                name: 'title',
                displayName: 'Title',
                render: nc => <SongTitle title={nc.song.title} songId={nc.song.id} isExplicit={nc.song.isExplicit}/>,
                width: '2fr',
                sortable: true,
                getValue: nc => nc.song.title,
            },
            {
                name: 'artists',
                displayName: 'Artists',
                render: nc => <SongArtists artists={nc.song.artists}/>,
                getValue: nc => nc.song.artists?.[0]?.name,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'album',
                displayName: 'Album',
                render: nc => <SongAlbum name={nc.song.album.name} albumId={nc.song.album.id}/>,
                getValue: nc => nc.song.album.name,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'waiver',
                displayName: 'Status',
                render: nc => nc.hasWaiver ? (
                    <Badge color="yellow" variant="light" size="sm">Waived</Badge>
                ) : (
                    <Badge color="gray" variant="light" size="sm">Pending</Badge>
                ),
                width: 80,
                align: 'center',
            },
            {
                name: 'createdAt',
                displayName: 'Detected',
                render: nc => new Date(nc.createdAt).toLocaleDateString(),
                sortable: true,
                getValue: nc => nc.createdAt,
                width: 100,
            },
        ],

        actions: (elems) => {
            const allHaveWaiver = elems.every(nc => nc.hasWaiver);
            const someHaveWaiver = elems.some(nc => nc.hasWaiver);
            const uniqueSongIds = [...new Set(elems.map(nc => nc.songId))];

            return [
                {group: "Edit"},
                {
                    name: "edit-songs",
                    renderIcon: () => <IconEdit/>,
                    renderLabel: () => `Edit ${uniqueSongIds.length === 1 ? 'Song' : `${uniqueSongIds.length} Songs`}`,
                    onClick: () => {
                        if (uniqueSongIds.length > 0) {
                            onEditSongs(uniqueSongIds);
                        }
                    },
                },
                {divider: true},
                {group: "Waiver"},
                !allHaveWaiver ? {
                    name: "grant-waiver",
                    renderIcon: () => <IconCheck/>,
                    renderLabel: () => `Grant Waiver`,
                    onClick: (items: ListAuditNonConformitiesItem[]) => {
                        const ids = items.filter(nc => !nc.hasWaiver).map(nc => nc.id);
                        if (ids.length > 0) {
                            onSetWaiver(ids, true, null);
                        }
                    },
                } : undefined,
                someHaveWaiver ? {
                    name: "remove-waiver",
                    renderIcon: () => <IconX/>,
                    renderLabel: () => `Remove Waiver`,
                    onClick: (items: ListAuditNonConformitiesItem[]) => {
                        const ids = items.filter(nc => nc.hasWaiver).map(nc => nc.id);
                        if (ids.length > 0) {
                            onSetWaiver(ids, false, null);
                        }
                    },
                } : undefined,
                {divider: true},
                {
                    name: "remove",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => `Remove`,
                    onClick: (items: ListAuditNonConformitiesItem[]) => {
                        const ids = items.map(nc => nc.id);
                        if (ids.length > 0) {
                            onDelete(ids);
                        }
                    },
                },
            ].filter((a): a is NonNullable<typeof a> => a !== undefined);
        },

        estimateListRowHeight: () => 64,
        renderListArtwork: (nc, size) => <SongArtwork id={nc.song.cover} size={size}/>,
        renderListTitle: (nc) => <SongTitle title={nc.song.title} songId={nc.song.id} isExplicit={nc.song.isExplicit}/>,
        renderListSubTitle: (nc) => (
            <Group gap="xs">
                <Text size="sm" c="dimmed" lineClamp={1}>
                    {nc.song.artists.map(a => a.name).join(', ')} • {nc.song.album.name}
                </Text>
                {nc.hasWaiver ? (
                    <Badge color="yellow" variant="light" size="xs">Waived</Badge>
                ) : (
                    <Badge color="gray" variant="light" size="xs">Pending</Badge>
                )}
            </Group>
        ),
    }) as CollectionSchema<ListAuditNonConformitiesItem>, [onSetWaiver, onDelete, onEditSongs]);
}
