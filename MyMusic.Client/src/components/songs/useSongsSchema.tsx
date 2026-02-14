import {Anchor} from "@mantine/core";
import {
    IconArrowForward,
    IconArrowRightDashed,
    IconDownload,
    IconHeart,
    IconHeartFilled,
    IconMusic,
    IconPlayerPlayFilled,
    IconPlaylistAdd
} from "@tabler/icons-react";
import {saveAs} from 'file-saver';
import {useMemo} from "react";
import {getDownloadSongUrl} from "../../client/songs.ts";
import type {PlayerAction, PlayerState} from "../../contexts/player-context.tsx";
import type {ListSongsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import SongAlbum from "../common/fields/song-album.tsx";
import SongArtists from "../common/fields/song-artists.tsx";
import SongArtwork from "../common/fields/song-artwork.tsx";
import SongSubTitle from "../common/fields/song-sub-title.tsx";
import SongTitle from "../common/fields/song-title.tsx";
import {usePlayHandler} from "../player/usePlayHandler.tsx";

export function useSongsSchema(playerStore: PlayerState & PlayerAction): CollectionSchema<ListSongsItem> {
    const playHandler = usePlayHandler(playerStore);

    return useMemo(() => ({
        key: row => row.id,
        searchVector: song => `${song.title} - ${song.artists.map(a => a.name).join(', ')} - ${song.album.name}`,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: row => <SongArtwork id={row.cover} onClick={ev => playHandler([row], ev)}/>,
                width: 52,
            },
            {
                name: 'title',
                displayName: 'Title',
                render: row => <SongTitle {...row} />,
                width: '2fr',
            },
            {
                name: 'artists',
                displayName: 'Artists',
                render: row => <SongArtists artists={row.artists}/>,
                width: '1fr',
            },
            {
                name: 'album',
                displayName: 'Album',
                render: row => <SongAlbum name={row.album.name}/>,
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
                {
                    name: 'download',
                    renderIcon: () => <IconDownload/>,
                    renderLabel: () => "Download",
                    onClick: (songs: ListSongsItem[]) => {
                        for (const song of songs) {
                            saveAs(getDownloadSongUrl(song.id));
                        }
                    }
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
                }
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.cover}
            size={size}
            placeholderIcon={<IconMusic/>}
            onClick={ev => playHandler([row], ev)}
        />,
        renderListTitle: (row, lineClamp) => <SongTitle {...row} lineClamp={lineClamp}/>,
        renderListSubTitle: (row) => <SongSubTitle c="gray" {...row} />,
    }) as CollectionSchema<ListSongsItem>, [playerStore]);
}