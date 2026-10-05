import {Anchor, Tooltip} from "@mantine/core";
import {IconArrowMerge, IconEdit, IconTrash, IconUserFilled} from "@tabler/icons-react";
import {Link} from "@tanstack/react-router";
import {useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import type {ListArtistItem} from "../../model";
import {TEXT_COLOR} from "../../utils/colors.ts";
import Artwork from "../common/artwork.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";
import {useConfirmDeleteArtists} from "./use-confirm-delete-artists.tsx";
import {useEditArtists} from "./use-edit-artists.ts";
import {useMergeArtistsModal} from "./use-merge-artists-modal.ts";


export function useArtistsSchema() {
    const {t} = useTranslation(["artists", "common"]);
    const confirmDeleteArtists = useConfirmDeleteArtists();
    const editArtists = useEditArtists();
    const mergeArtists = useMergeArtistsModal();
    const {data: filterMetadata} = useFilterMetadata('artists');

    const fetchFilterValues = useCallback(async (field: string, searchTerm: string) => {
        const params = new URLSearchParams({field, limit: "15"});
        if (searchTerm) params.set("search", searchTerm);
        const response = await fetch(`/api/artists/filter-values?${params}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.values as string[];
    }, []);

    return useMemo(() => ({
        key: row => row.id,
        searchVector: artist => artist.name,
        filterMetadata,
        fetchFilterValues,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'photo',
                displayName: '',
                render: row =>
                    <Artwork
                        id={row.photo}
                        size={32}
                        placeholderIcon={<IconUserFilled/>}
                    />,
                width: '52px',
            },
            {
                name: 'name',
                displayName: t("artists:schema.columns.name"),
                render: row =>
                    <Tooltip label={row.name} openDelay={500}>
                        <Anchor component={Link} to={`/artists/${row.id}`} c={TEXT_COLOR}>{row.name}</Anchor>
                    </Tooltip>,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'albumsCount',
                displayName: t("artists:schema.columns.albums"),
                render: row => row.albumsCount,
                width: '60px',
                align: 'center',
                sortable: true,
            },
            {
                name: 'songsCount',
                displayName: t("artists:schema.columns.songs"),
                render: row => row.songsCount,
                width: '60px',
                align: 'center',
                sortable: true,
            },
            {
                name: 'createdAt',
                displayName: t("artists:schema.columns.createdAt"),
                render: row => row.createdAt,
                sortable: true,
                hidden: true,
                getValue: artist => artist.createdAt,
            }
        ],

        actions: (elems) => {
            if (elems.length === 0) {
                return [];
            }

            return [
                {
                    name: "edit",
                    renderIcon: () => <IconEdit/>,
                    renderLabel: () => elems.length === 1
                        ? t("artists:editModal.titleSingle")
                        : t("artists:editModal.titlePlural", {count: elems.length}),
                    onClick: (artists: ListArtistItem[]) => editArtists(artists),
                },
                // Merging takes at least two artists: the one that is kept, and the ones merged into it
                ...(elems.length > 1 ? [{
                    name: "merge",
                    renderIcon: () => <IconArrowMerge/>,
                    renderLabel: () => t("artists:merge.title", {count: elems.length}),
                    onClick: (artists: ListArtistItem[]) => mergeArtists(artists),
                }] : []),
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => elems.length === 1
                        ? t("artists:delete.titleSingle")
                        : t("artists:delete.titlePlural", {count: elems.length}),
                    onClick: (artists: ListArtistItem[]) => confirmDeleteArtists(artists),
                },
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.photo}
            size={size}
            placeholderIcon={<IconUserFilled/>}
        />,
        renderListTitle: (row) => <Tooltip label={row.name} openDelay={500}>
            <Anchor component={Link} to={`/artists/${row.id}`} c={TEXT_COLOR}>{row.name}</Anchor>
        </Tooltip>,
        renderListSubTitle: (row) => t("artists:schema.albumsCount", {count: row.albumsCount}),
    }) as CollectionSchema<ListArtistItem>, [filterMetadata, fetchFilterValues, t, confirmDeleteArtists, editArtists, mergeArtists]);
}