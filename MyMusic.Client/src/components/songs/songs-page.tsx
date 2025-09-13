import Collection, {type CollectionSchema} from "../common/collection.tsx";
import {Anchor, Tooltip} from "@mantine/core";
import {useListSongs} from '../../client/songs.ts';
import {useEffect} from "react";
import Artwork from "../common/artwork.tsx";
import {IconMusic} from "@tabler/icons-react";
import type {Song} from "../../model";
import {usePlayerContext} from "../../contexts/player-context.tsx";
import ExplicitLabel from "../common/explicit-label.tsx";

export default function SongsPage() {
    const playerStore = usePlayerContext();
    const {data: songs, refetch} = useListSongs();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const songsSchema = {
        key: row => row.id,
        estimateRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: row =>
                    <Artwork
                        id={row.cover}
                        size={32}
                        placeholderIcon={<IconMusic/>}
                        onClick={ev => {
                            if (ev.ctrlKey) {
                                playerStore.playLast([row]);
                            } else if (ev.shiftKey) {
                                playerStore.playNext([row]);
                            } else {
                                playerStore.play([row]);
                            }
                        }}
                    />,
                width: 52,
            },
            {
                name: 'title',
                displayName: 'Title',
                render: row =>
                    <ExplicitLabel visible={row.isExplicit}>
                        <Tooltip label={row.title} openDelay={500}>
                            <Anchor c={"black"}>{row.title}</Anchor>
                        </Tooltip>
                    </ExplicitLabel>,
                width: '25%',
            },
            {
                name: 'artists',
                displayName: 'Artists',
                render: row => row.artists.map(((artist, i) => <>
                    {i > 0 && ', '}
                    <Anchor key={artist.id} c={"black"}>{artist.name}</Anchor>
                </>)),
                width: '20%',
            },
            {
                name: 'album',
                displayName: 'Album',
                render: row => <Anchor c={"black"}>{row.album.name}</Anchor>,
                width: '20%',
            },
            {
                name: 'genres',
                displayName: 'Genres',
                render: row => row.genres.map(((genre, i) => <>
                    {i > 0 && ', '}
                    <Anchor key={genre.id} c={"black"}>{genre.name}</Anchor>
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

    const elements = songs?.data?.songs ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    </>;
}
