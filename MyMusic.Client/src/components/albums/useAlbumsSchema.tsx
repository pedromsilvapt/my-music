import {Anchor, Tooltip} from "@mantine/core";
import {IconTrash, IconUserFilled} from "@tabler/icons-react";
import {Link} from "@tanstack/react-router";
import {useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import type {ListAlbumItem} from "../../model";
import {TEXT_COLOR} from "../../utils/colors.ts";
import Artwork from "../common/artwork.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";
import {useConfirmDeleteAlbums} from "./use-confirm-delete-albums.tsx";

export interface AlbumsSchemaOptions {
    /** Albums of another user (a shared view) cannot be changed: their actions are hidden. */
    readOnly?: boolean;
}

export function useAlbumsSchema({readOnly = false}: AlbumsSchemaOptions = {}) {
    const {t} = useTranslation(["albums", "common"]);
    const confirmDeleteAlbums = useConfirmDeleteAlbums();
    const {data: filterMetadata} = useFilterMetadata('albums');

    const fetchFilterValues = useCallback(async (field: string, searchTerm: string) => {
        const params = new URLSearchParams({field, limit: "15"});
        if (searchTerm) params.set("search", searchTerm);
        const response = await fetch(`/api/albums/filter-values?${params}`);
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
                name: 'artwork',
                displayName: '',
                render: row =>
                    <Artwork
                        id={row.cover}
                        size={32}
                        placeholderIcon={<IconUserFilled/>}
                    />,
                width: '52px',
            },
            {
                name: 'name',
                displayName: t("albums:schema.columns.name"),
                render: row =>
                    <Tooltip label={row.name} openDelay={500}>
                        <Anchor component={Link} to={`/albums/${row.id}`} c={TEXT_COLOR}>{row.name}</Anchor>
                    </Tooltip>,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'year',
                displayName: t("albums:schema.columns.year"),
                render: row => row.year,
                width: '60px',
                align: 'center',
                sortable: true,
            },
            {
                name: 'songsCount',
                displayName: t("albums:schema.columns.songs"),
                render: row => row.songsCount,
                width: '60px',
                align: 'center',
                sortable: true,
            },
            {
                name: 'createdAt',
                displayName: t("albums:schema.columns.createdAt"),
                render: row => row.createdAt,
                sortable: true,
                hidden: true,
                getValue: album => album.createdAt,
            }
        ],

        actions: (elems) => {
            if (readOnly || elems.length === 0) {
                return [];
            }

            return [
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => elems.length === 1
                        ? t("albums:delete.titleSingle")
                        : t("albums:delete.titlePlural", {count: elems.length}),
                    onClick: (albums: ListAlbumItem[]) => confirmDeleteAlbums(albums),
                },
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.cover}
            size={size}
            placeholderIcon={<IconUserFilled/>}
        />,
        renderListTitle: (row) => <Tooltip label={row.name} openDelay={500}>
            <Anchor component={Link} to={`/albums/${row.id}`} c={TEXT_COLOR}>{row.name}</Anchor>
        </Tooltip>,
        renderListSubTitle: (row) => t("albums:schema.songsCount", {count: row.songsCount}),
    }) as CollectionSchema<ListAlbumItem>, [filterMetadata, fetchFilterValues, t, readOnly, confirmDeleteAlbums]);
}