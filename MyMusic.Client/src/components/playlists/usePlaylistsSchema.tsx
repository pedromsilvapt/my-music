import {Anchor, Group, Text, Tooltip} from "@mantine/core";
import {IconPlaylist, IconShare, IconTrash} from "@tabler/icons-react";
import {Link} from "@tanstack/react-router";
import {useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useDeletePlaylist} from "../../client/playlists.ts";
import {useManageSharingContext} from "../../contexts/manage-sharing-context.tsx";
import type {ListPlaylistItem} from "../../model";
import {TEXT_COLOR} from "../../utils/colors.ts";
import Artwork from "../common/artwork.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";
import PlaylistShareIndicator from "./playlist-share-indicator.tsx";

export function usePlaylistsSchema() {
    const {t} = useTranslation(["playlists", "common"]);
    const deletePlaylist = useDeletePlaylist();
    const {open: openManageSharing} = useManageSharingContext();
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
                render: () =>
                    <Artwork
                        id={null}
                        size={32}
                        placeholderIcon={<IconPlaylist/>}
                    />,
                width: 52,
            },
            {
                name: 'name',
                displayName: t("playlists:schema.columns.name"),
                render: row =>
                    <Group gap={6} wrap="nowrap" data-testid="playlist-title" data-playlist-name={row.name}>
                        <Tooltip label={row.name} openDelay={500}>
                            <Anchor component={Link} to={`/playlists/${row.id}`} c={TEXT_COLOR} truncate>{row.name}</Anchor>
                        </Tooltip>
                        <PlaylistShareIndicator playlist={row}/>
                    </Group>,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'songCount',
                displayName: t("playlists:schema.columns.songs"),
                render: row => row.songCount,
                width: 80,
                align: 'center',
                sortable: true,
            },
            {
                name: 'createdAt',
                displayName: t("playlists:schema.columns.created"),
                render: row => row.createdAt,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'modifiedAt',
                displayName: t("playlists:schema.columns.modified"),
                render: row => row.modifiedAt ?? '',
                width: '1fr',
                sortable: true,
                hidden: true,
            }
        ],

        actions: (elems) => {
            // Playlists shared with me are read-only: sharing and deleting only apply to owned ones
            const allOwned = elems.every(p => !p.isSharedWithMe);

            return allOwned ? [
                {group: t("playlists:schema.manageGroup")},
                {
                    name: "share",
                    renderIcon: () => <IconShare/>,
                    renderLabel: () => t("playlists:schema.share"),
                    onClick: (playlists: ListPlaylistItem[]) => {
                        openManageSharing(playlists.map(p => p.id));
                    },
                },
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => t("common:actions.delete"),
                    onClick: (playlists: ListPlaylistItem[]) => {
                        for (const playlist of playlists) {
                            deletePlaylist.mutate({id: playlist.id});
                        }
                    },
                }
            ] : [];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (_row, size) => <Artwork
            id={null}
            size={size}
            placeholderIcon={<IconPlaylist/>}
        />,
        renderListTitle: (row) => <Group gap={6} wrap="nowrap" data-testid="playlist-title" data-playlist-name={row.name}>
            <Tooltip label={row.name} openDelay={500}>
                <Anchor component={Link} to={`/playlists/${row.id}`} c={TEXT_COLOR} truncate>{row.name}</Anchor>
            </Tooltip>
            <PlaylistShareIndicator playlist={row}/>
        </Group>,
        renderListSubTitle: (row) => <Text c="gray">
            {row.isSharedWithMe
                ? `${t("common:count.songs", {count: row.songCount})} • ${t("playlists:share.sharedBy", {name: row.ownerName})}`
                : t("common:count.songs", {count: row.songCount})}
        </Text>,
    }) as CollectionSchema<ListPlaylistItem>, [deletePlaylist, openManageSharing, filterMetadata, fetchFilterValues, t]);
}
