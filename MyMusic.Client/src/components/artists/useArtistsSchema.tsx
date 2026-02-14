import {Anchor, Tooltip} from "@mantine/core";
import {IconUserFilled} from "@tabler/icons-react";
import {Link} from "@tanstack/react-router";
import {useMemo} from "react";
import type {ListArtistsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";


export function useArtistsSchema() {
    return useMemo(() => ({
        key: row => row.id,
        searchVector: artist => artist.name,

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
                displayName: 'Name',
                render: row =>
                    <Tooltip label={row.name} openDelay={500}>
                        <Anchor component={Link} to={`/artists/${row.id}`} c={"black"}>{row.name}</Anchor>
                    </Tooltip>,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'albumsCount',
                displayName: 'Albums',
                render: row => row.albumsCount,
                width: '60px',
                align: 'center',
                sortable: true,
            },
            {
                name: 'songsCount',
                displayName: 'Songs',
                render: row => row.songsCount,
                width: '60px',
                align: 'center',
                sortable: true,
            },
            {
                name: 'createdAt',
                displayName: 'Created At',
                render: row => row.createdAt,
                sortable: true,
                hidden: true,
                getValue: artist => artist.createdAt,
            }
        ],

        actions: () => {
            return [];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.photo}
            size={size}
            placeholderIcon={<IconUserFilled/>}
        />,
        renderListTitle: (row) => <Tooltip label={row.name} openDelay={500}>
            <Anchor component={Link} to={`/artists/${row.id}`} c={"black"}>{row.name}</Anchor>
        </Tooltip>,
        renderListSubTitle: (row) => row.albumsCount + ' albums',
    }) as CollectionSchema<ListArtistsItem>, []);
}