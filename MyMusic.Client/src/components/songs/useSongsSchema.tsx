import {Anchor, Text} from "@mantine/core";
import {
    IconArrowForward,
    IconArrowRightDashed,
    IconArrowsShuffle,
    IconDevicesCog,
    IconDownload,
    IconEdit,
    IconHeart,
    IconHeartFilled,
    IconMusic,
    IconPlayerPlayFilled,
    IconPlaylistAdd,
    IconX
} from "@tabler/icons-react";
import {saveAs} from 'file-saver';
import {modals} from '@mantine/modals';
import {SONG_EDITOR_MODAL_SIZE} from "../../consts.ts";
import {useCallback, useMemo} from "react";
import {getDownloadSongUrl} from "../../client/songs";
import {useGetDevices} from "../../client/devices";
import {useManageDevicesContext} from "../../contexts/manage-devices-context";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context";
import {useCurrentSongId, useQueue, useQueueMutations} from "../../contexts/player-context";
import {useToggleFavorites} from "../../hooks/use-favorites";
import type {ListSongItem} from "../../model";
import {usePlaybackStoreApi} from "../../stores/playback-store";
import {TEXT_COLOR} from "../../utils/colors.ts";
import {isGetPlaylistSong} from "../../utils/type-guards";
import Artwork from "../common/artwork";
import type {CollectionSchema} from "../common/collection/collection";
import SongAlbum from "../common/fields/song-album";
import SongArtists from "../common/fields/song-artists";
import SongArtwork from "../common/fields/song-artwork";
import SongSubTitle from "../common/fields/song-sub-title";
import SongTitle from "../common/fields/song-title";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";
import {usePlayHandler} from "../player/usePlayHandler";
import {SongDevicesCell} from "./song-devices-cell";

export function useSongsSchema(nowPlaying: boolean = false): CollectionSchema<ListSongItem> {
    const {play, playNext, playLast, removeByIndices, shuffleByIndices} = useQueueMutations();
    const playbackStore = usePlaybackStoreApi();
    const currentSongId = useCurrentSongId();
    const {currentSongId: queueCurrentSongId} = useQueue();

    const toggleFavorites = useToggleFavorites({
        mutation: {
            onSuccess: (data) => {
                for (const song of data.data.songs) {
                    playbackStore.getState().setIsFavorite(song.isFavorite, song.id);
                }
            }
        }
    });
    const playHandler = usePlayHandler(nowPlaying);
    const {open: openManagePlaylists} = useManagePlaylistsContext();
    const {open: openManageDevices} = useManageDevicesContext();
    const {data: filterMetadata} = useFilterMetadata('songs');

    const {data: devicesData} = useGetDevices();
    const allDevices = useMemo(() => devicesData?.data.devices ?? [], [devicesData]);
    
    const fetchFilterValues = useCallback(async (field: string, searchTerm: string) => {
        const params = new URLSearchParams({field, limit: "15"});
        if (searchTerm) params.set("search", searchTerm);
        const response = await fetch(`/api/songs/filter-values?${params}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.values as string[];
    }, []);

    return useMemo(() => ({
        key: row => row.id,
        searchVector: song => `${song.title} - ${song.artists.map(a => a.name).join(', ')} - ${song.album.name}`,
        filterMetadata,
        fetchFilterValues,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            ...(nowPlaying ? [{
                name: 'position',
                displayName: '',
                render: (row: ListSongItem & { order?: number }) => <Text c="dimmed">#{(row.order ?? 0) + 1}</Text>,
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
                    <Anchor key={genre.id} c={TEXT_COLOR}>{genre.name}</Anchor>
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
                name: 'devices',
                displayName: 'Devices',
                render: row => <SongDevicesCell song={row} allDevices={allDevices} />,
                align: 'center',
                width: 'auto',
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
                    onClick: (songs: ListSongItem[]) => {
                        toggleFavorites.mutate({data: {ids: songs.map(s => s.id)}});
                    },
                },
                {
                    name: "manage-playlists",
                    renderIcon: () => <IconPlaylistAdd/>,
                    renderLabel: () => "Manage Playlists",
                    onClick: (songs: ListSongItem[]) => {
                        openManagePlaylists(songs.map(s => s.id));
                    },
                },
                {
                    name: "manage-devices",
                    renderIcon: () => <IconDevicesCog/>,
                    renderLabel: () => "Manage Devices",
                    onClick: (songs: ListSongItem[]) => {
                        openManageDevices(songs.map(s => s.id));
                    },
                },
                {
                    name: "edit",
                    renderIcon: () => <IconEdit/>,
                    renderLabel: () => `Edit ${elems.length === 1 ? 'Song' : `${elems.length} Songs`}`,
                    onClick: (songs: ListSongItem[]) => {
                        modals.openContextModal({
                            modal: 'song-editor',
                            title: 'Edit Song',
                            size: SONG_EDITOR_MODAL_SIZE,
                            innerProps: { songIds: songs.map(s => s.id) },
                        });
                    },
                },
                {
                    name: 'download',
                    renderIcon: () => <IconDownload/>,
                    renderLabel: () => "Download",
                    onClick: (songs: ListSongItem[]) => {
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
                    onClick: (songs: ListSongItem[]) => play(songs),
                },
                {
                    name: "play-next",
                    renderIcon: () => <IconArrowRightDashed/>,
                    renderLabel: () => "Play Next",
                    onClick: (songs: ListSongItem[]) => playNext(songs),
                },
                {
                    name: "play-last",
                    renderIcon: () => <IconArrowForward/>,
                    renderLabel: () => "Play Last",
                    onClick: (songs: ListSongItem[]) => playLast(songs),
                },
                ...(nowPlaying ? [
                    {
                        name: "shuffle",
                        renderIcon: () => <IconArrowsShuffle/>,
                        renderLabel: () => "Shuffle",
                        onClick: (songs: ListSongItem[]) => {
                            const indices = songs
                                .map(s => isGetPlaylistSong(s) ? s.order : -1)
                                .filter((i): i is number => i >= 0);
                            shuffleByIndices(indices);
                        },
                    },
                    {
                        name: "remove-from-queue",
                        renderIcon: () => <IconX/>,
                        renderLabel: () => "Remove from Queue",
                        onClick: (songs: ListSongItem[]) => {
                            const indices = songs
                                .map(s => isGetPlaylistSong(s) ? s.order : -1)
                                .filter((i): i is number => i >= 0);
                            removeByIndices(indices, queueCurrentSongId);
                        },
                    }
                ] : [])
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
    }) as CollectionSchema<ListSongItem>, [play, playNext, playLast, removeByIndices, shuffleByIndices, nowPlaying, currentSongId, playHandler, openManagePlaylists, openManageDevices, toggleFavorites, queueCurrentSongId, filterMetadata, fetchFilterValues, allDevices]);
}
