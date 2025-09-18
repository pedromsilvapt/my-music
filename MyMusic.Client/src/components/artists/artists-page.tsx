import {Anchor, Tooltip} from "@mantine/core";
import {IconUserFilled} from "@tabler/icons-react";
import {useEffect} from "react";
import {useListArtists} from "../../client/artists.ts";
import type {ListArtistsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import Collection, {type CollectionSchema} from "../common/collection/collection.tsx";

export default function ArtistsPage() {
    const {data: artists, refetch} = useListArtists();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const artistsSchema = {
        key: row => row.id,
        searchVector: artist => artist.name,

        estimateRowHeight: () => 47 * 2,
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
                        <Anchor c={"black"}>{row.name}</Anchor>
                    </Tooltip>,
                width: '1fr',
            },
            {
                name: 'albums',
                displayName: 'Albums',
                render: row => row.albumsCount,
                width: '60px',
                align: 'center',
            },
            {
                name: 'songs',
                displayName: 'Songs',
                render: row => row.songsCount,
                width: '60px',
                align: 'center',
            },
        ],

        actions: () => {
            return [];
        }
    } as CollectionSchema<ListArtistsItem>;

    const elements = artists?.data?.artists ?? [];

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
