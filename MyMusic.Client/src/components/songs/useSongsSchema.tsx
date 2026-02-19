import {Anchor, Text} from "@mantine/core";
import {
    IconArrowForward,
    IconArrowRightDashed,
    IconDevicesCog,
    IconDownload,
    IconHeart,
    IconHeartFilled,
    IconMusic,
    IconPlayerPlayFilled,
    IconPlaylistAdd,
    IconX
} from "@tabler/icons-react";
import {saveAs} from 'file-saver';
import {useMemo} from "react";
import {getDownloadSongUrl} from "../../client/songs.ts";
import {useManageDevicesContext} from "../../contexts/manage-devices-context.tsx";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {usePlayerActions} from "../../contexts/player-context.tsx";
import {useToggleFavorites} from "../../hooks/use-favorites.ts";
import type {ListSongsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import SongAlbum from "../common/fields/song-album.tsx";
import SongArtists from "../common/fields/song-artists.tsx";
import SongArtwork from "../common/fields/song-artwork.tsx";
import SongSubTitle from "../common/fields/song-sub-title.tsx";
import SongTitle from "../common/fields/song-title.tsx";
import {useCurrentSong} from "../player/now-playing-page.tsx";
import {usePlayHandler} from "../player/usePlayHandler.tsx";

export function useSongsSchema(nowPlaying: boolean = false): CollectionSchema<ListSongsItem> {
    const playerActions = usePlayerActions();
    const currentSongId = useCurrentSong()?.id;
    const toggleFavorites = useToggleFavorites({
        mutation: {
            onSuccess: (data) => {
                for (const song of data.data.songs) {
                    playerActions.setIsFavorite(song.isFavorite, song.id);
                }
            }
        }
    });
    const playHandler = usePlayHandler(playerActions, nowPlaying);
    const {open: openManagePlaylists} = useManagePlaylistsContext();
    const {open: openManageDevices} = useManageDevicesContext();

    return useMemo(() => ({
        key: row => row.id,
        searchVector: song => `${song.title} - ${song.artists.map(a => a.name).join(', ')} - ${song.album.name}`,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            ...(nowPlaying ? [{
                name: 'position',
                displayName: '',
                render: (row: ListSongsItem & { order?: number }) => <Text c="dimmed">#{(row.order ?? 0) + 1}</Text>,
                align: 'center',
                width: 40,
            }] : []),
            {
                name: 'artwork',
                displayName: '',
                render: row => <SongArtwork id={row.cover} onClick={ev => playHandler([row], ev)}/>,
                width: 52,
            },
            {
                name: 'title',
                displayName: 'Title',
                render: row => <SongTitle
                    title={row.title}
                    songId={row.id}
                    isExplicit={row.isExplicit}
                    isPlaying={nowPlaying && currentSongId === row.id}
                />,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'artists',
                displayName: 'Artists',
                render: row => <SongArtists artists={row.artists}/>,
                getValue: song => song.artists?.[0].name,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'album',
                displayName: 'Album',
                render: row => <SongAlbum name={row.album.name} albumId={row.album.id}/>,
                getValue: song => song.album.name,
                width: '1fr',
                sortable: true,
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
                sortable: true,
            },
            {
                name: 'duration',
                displayName: 'Duration',
                render: row => row.duration,
                align: 'right',
                sortable: true,
            },
            {
                name: 'createdAt',
                displayName: 'Created At',
                render: row => row.createdAt,
                sortable: true,
                hidden: true,
                getValue: song => song.createdAt,
            },
            {
                name: 'addedAt',
                displayName: 'Added At',
                render: row => row.addedAt ?? '',
                sortable: true,
                hidden: true,
                getValue: song => song.addedAt ?? '',
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
                    onClick: (songs: ListSongsItem[]) => {
                        toggleFavorites.mutate({data: {ids: songs.map(s => s.id)}});
                    },
                },
                {
                    name: "manage-playlists",
                    renderIcon: () => <IconPlaylistAdd/>,
                    renderLabel: () => "Manage Playlists",
                    onClick: (songs: ListSongsItem[]) => {
                        openManagePlaylists(songs.map(s => s.id));
                    },
                },
                {
                    name: "manage-devices",
                    renderIcon: () => <IconDevicesCog/>,
                    renderLabel: () => "Manage Devices",
                    onClick: (songs: ListSongsItem[]) => {
                        openManageDevices(songs.map(s => s.id));
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
                    onClick: (songs: ListSongsItem[]) => playerActions.play(songs),
                },
                {
                    name: "play-next",
                    renderIcon: () => <IconArrowRightDashed/>,
                    renderLabel: () => "Play Next",
                    onClick: (songs: ListSongsItem[]) => playerActions.playNext(songs),
                },
                {
                    name: "play-last",
                    renderIcon: () => <IconArrowForward/>,
                    renderLabel: () => "Play Last",
                    onClick: (songs: ListSongsItem[]) => playerActions.playLast(songs),
                },
                ...(nowPlaying ? [{
                    name: "remove-from-queue",
                    renderIcon: () => <IconX/>,
                    renderLabel: () => "Remove from Queue",
                    onClick: (songs: ListSongsItem[]) => {
                        const indices = songs
                            .map(s => 'order' in s ? (s as { order: number }).order : -1)
                            .filter((i): i is number => i >= 0);
                        playerActions.removeFromQueue(indices);
                    },
                }] : [])
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <Artwork
            id={row.cover}
            size={size}
            placeholderIcon={<IconMusic/>}
            onClick={ev => playHandler([row], ev)}
        />,
        renderListTitle: (row, lineClamp) => <SongTitle title={row.title} songId={row.id} isExplicit={row.isExplicit}
                                                        lineClamp={lineClamp}/>,
        renderListSubTitle: (row) => <SongSubTitle c="gray" {...row} />,
    }) as CollectionSchema<ListSongsItem>, [playerActions, nowPlaying, currentSongId, playHandler, openManagePlaylists, openManageDevices, toggleFavorites]);
}