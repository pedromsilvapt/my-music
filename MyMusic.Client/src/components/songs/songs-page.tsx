import Collection, {type CollectionSchema} from "../common/collection.tsx";
import {Anchor, Tooltip} from "@mantine/core";
import {useListSongs} from '../../client/songs.ts';
import {useEffect} from "react";
import Artwork from "../common/artwork.tsx";
import {IconMusic} from "@tabler/icons-react";
import type {Song} from "../../model";

const songsSchema = {
    key: row => row.id,
    estimateRowHeight: () => 47 * 2,
    columns: [
        {
            name: 'artwork',
            displayName: '',
            render: row => <Artwork id={row.cover} size={32} placeholderIcon={<IconMusic/>}/>,
            width: 32,
        },
        {
            name: 'title',
            displayName: 'Title',
            render: row => <Tooltip label={row.title} openDelay={500}><Anchor>{row.title}</Anchor></Tooltip>,
            width: '25%',
        },
        {
            name: 'artists',
            displayName: 'Artists',
            render: row => row.artists.map(((artist, i) => <>
                {i > 0 && ', '}
                <Anchor>{artist.name}</Anchor>
            </>)),
            width: '20%',
        },
        {
            name: 'album',
            displayName: 'Album',
            render: row => <Anchor>{row.album.name}</Anchor>,
            width: '20%',
        },
        {
            name: 'genres',
            displayName: 'Genres',
            render: row => row.genres.map(((genre, i) => <>
                {i > 0 && ', '}
                <Anchor>{genre.name}</Anchor>
            </>)),
            width: '20%',
        },
        {
            name: 'year',
            displayName: 'Year',
            render: row => row.year,
        },
        {
            name: 'duration',
            displayName: 'Duration',
            render: row => row.duration,
        }
    ]
} as CollectionSchema<Song>;

export default function SongsPage() {
    const {data: songs, refetch} = useListSongs();

    const elements = songs?.data?.songs ?? [];

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    </>;
}
