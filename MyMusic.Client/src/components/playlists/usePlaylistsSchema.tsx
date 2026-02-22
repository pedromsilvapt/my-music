import {Anchor, Text, Tooltip} from "@mantine/core";
import {IconPlaylist, IconTrash} from "@tabler/icons-react";
import {Link} from "@tanstack/react-router";
import {useCallback, useMemo} from "react";
import {useDeletePlaylist} from "../../client/playlists.ts";
import type {ListPlaylistItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";

export function usePlaylistsSchema() {
    const deletePlaylist = useDeletePlaylist();
    const {data: filterMetadata} = useFilterMetadata('playlists');

    const fetchFilterValues = useCallback(async (field: string, searchTerm: string) => {
        const params = new URLSearchParams({field, limit: "15"});
        if (searchTerm) params.set("search", searchTerm);
        const response = await fetch(`/api/playlists/filter-values?${params}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.values as string[];
    }, []);

    return useMemo(() => ({
        key: row => row.id,
        searchVector: playlist => playlist.name,
        filterMetadata,
        fetchFilterValues,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: _row =>
                    <Artwork
                        id={null}
                        size={32}
                        placeholderIcon={<IconPlaylist/>}
                    />,
                width: 52,
            },
            {
                name: 'name',
                displayName: 'Name',
                render: row =>
                    <Tooltip label={row.name} openDelay={500}>
                        <Anchor component={Link} to={`/playlists/${row.id}`} c={"black"}>{row.name}</Anchor>
                    </Tooltip>,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'songCount',
                displayName: 'Songs',
                render: row => row.songCount,
                width: 80,
                align: 'center',
                sortable: true,
            },
            {
                name: 'createdAt',
                displayName: 'Created',
                render: row => row.createdAt,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'modifiedAt',
                displayName: 'Modified',
                render: row => row.modifiedAt ?? '',
                width: '1fr',
                sortable: true,
                hidden: true,
            }
        ],

        actions: (_elems: ListPlaylistItem[]) => {
            return [
                {group: "Manage"},
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => "Delete",
                    onClick: (playlists: ListPlaylistItem[]) => {
                        console.log(playlists);
                        for (const playlist of playlists) {
                            deletePlaylist.mutate({id: playlist.id});
                        }
                    },
                }
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (_row, size) => <Artwork
            id={null}
            size={size}
            placeholderIcon={<IconPlaylist/>}
        />,
        renderListTitle: (row) => <Tooltip label={row.name} openDelay={500}>
            <Anchor component={Link} to={`/playlists/${row.id}`} c={"black"}>{row.name}</Anchor>
        </Tooltip>,
        renderListSubTitle: (row) => <Text c="gray">{row.songCount} songs</Text>,
    }) as CollectionSchema<ListPlaylistItem>, [deletePlaylist, filterMetadata, fetchFilterValues]);
}
