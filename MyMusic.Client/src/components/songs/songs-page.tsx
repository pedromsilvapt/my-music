import {Anchor, Tooltip} from "@mantine/core";
import {
    IconArrowForward,
    IconArrowRightDashed,
    IconHeart,
    IconHeartFilled,
    IconMusic,
    IconPlayerPlayFilled,
    IconPlaylistAdd
} from "@tabler/icons-react";
import {useEffect} from "react";
import {useListSongs} from '../../client/songs.ts';
import {usePlayerContext} from "../../contexts/player-context.tsx";
import type {ListSongsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import Collection, {type CollectionSchema} from "../common/collection/collection.tsx";
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
        searchVector: song => `${song.title} - ${song.artists.map(a => a.name).join(', ')} - ${song.album.name}`,

        estimateTableRowHeight: () => 47 * 2,
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
                            ev.stopPropagation();
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
                            <Anchor lineClamp={1} c={"black"}>{row.title}</Anchor>
                        </Tooltip>
                    </ExplicitLabel>,
                width: '2fr',
            },
            {
                name: 'artists',
                displayName: 'Artists',
                render: row => row.artists.map(((artist, i) => <>
                    {i > 0 && ', '}
                    <Anchor key={artist.id} c={"black"}>{artist.name}</Anchor>
                </>)),
                width: '1fr',
            },
            {
                name: 'album',
                displayName: 'Album',
                render: row => <Anchor c={"black"}>{row.album.name}</Anchor>,
                width: '1fr',
            },
            {
                name: 'genres',
                displayName: 'Genres',
                render: row => row.genres.map(((genre, i) => <>
                    {i > 0 && ', '}
                    <Anchor key={genre.id} c={"black"}>{genre.name}</Anchor>
                </>)),
                width: '1fr',
            },
            {
                name: 'year',
                displayName: 'Year',
                render: row => row.year,
                align: 'center',
            },
            {
                name: 'duration',
                displayName: 'Duration',
                render: row => row.duration,
                align: 'right',
            }
        ],

        actions: (elems) => {
            const allAreFavorites = elems.every(s => s.isFavorite);

            return [
                {group: "Manage"},
                {
                    name: "favorite",
                    renderIcon: () => allAreFavorites ? <IconHeartFilled/> : <IconHeart/>,
                    renderLabel: () => allAreFavorites ? "Unfavorite" : "Favorite",
                    onClick: () => {
                    },
                },
                {
                    name: "add-to-playlists",
                    renderIcon: () => <IconPlaylistAdd/>,
                    renderLabel: () => "Add to Playlists",
                    onClick: () => {
                    },
                },
                {group: "Queue"},
                {
                    name: "play",
                    renderIcon: () => <IconPlayerPlayFilled/>,
                    renderLabel: () => "Play",
                    onClick: (songs: ListSongsItem[]) => playerStore.play(songs),
                },
                {
                    name: "play-next",
                    renderIcon: () => <IconArrowRightDashed/>,
                    renderLabel: () => "Play Next",
                    onClick: (songs: ListSongsItem[]) => playerStore.playNext(songs),
                },
                {
                    name: "play-last",
                    renderIcon: () => <IconArrowForward/>,
                    renderLabel: () => "Play Last",
                    onClick: (songs: ListSongsItem[]) => playerStore.playLast(songs),
                },
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.cover}
            size={size}
            placeholderIcon={<IconMusic/>}
            onClick={ev => {
                ev.stopPropagation();
                if (ev.ctrlKey) {
                    playerStore.playLast([row]);
                } else if (ev.shiftKey) {
                    playerStore.playNext([row]);
                } else {
                    playerStore.play([row]);
                }
            }}
        />,
        renderListTitle: (row, lineClamp) => <ExplicitLabel visible={row.isExplicit}>
            <Tooltip label={row.title} openDelay={500}>
                <Anchor lineClamp={lineClamp} c={"black"}>{row.title}</Anchor>
            </Tooltip>
        </ExplicitLabel>,
        renderListSubTitle: (row) => row.album?.name,
    } as CollectionSchema<ListSongsItem>;

    const elements = songs?.data?.songs ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    </>;
}
