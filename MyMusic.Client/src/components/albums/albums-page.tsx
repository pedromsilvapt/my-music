import {Anchor, Tooltip} from "@mantine/core";
import {IconUserFilled} from "@tabler/icons-react";
import {useEffect} from "react";
import {useListAlbums} from "../../client/albums.ts";
import type {ListAlbumsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import Collection, {type CollectionSchema} from "../common/collection/collection.tsx";

export default function AlbumsPage() {
    const {data: albums, refetch} = useListAlbums();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const artistsSchema = {
        key: row => row.id,
        searchVector: artist => artist.name,

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
                displayName: 'Name',
                render: row =>
                    <Tooltip label={row.name} openDelay={500}>
                        <Anchor c={"black"}>{row.name}</Anchor>
                    </Tooltip>,
                width: '1fr',
            },
            {
                name: 'year',
                displayName: 'Year',
                render: row => row.year,
                width: '60px',
                align: 'center',
            },
            {
                name: 'songs',
                displayName: 'Songs',
                render: row => row.songsCount,
                width: '60px',
                align: 'center',
            }
        ],

        actions: () => {
            return [];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.cover}
            size={size}
            placeholderIcon={<IconUserFilled/>}
        />,
        renderListTitle: (row) => <Tooltip label={row.name} openDelay={500}>
            <Anchor c={"black"}>{row.name}</Anchor>
        </Tooltip>,
        renderListSubTitle: (row) => row.songsCount + ' songs',
    } as CollectionSchema<ListAlbumsItem>;

    const elements = albums?.data?.albums ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="artists"
                items={elements}
                schema={artistsSchema}>
            </Collection>
        </div>
    </>;
}
