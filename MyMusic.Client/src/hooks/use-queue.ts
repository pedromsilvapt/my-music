import {useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {
    getGetQueueQueryKey,
    useAddToQueue,
    useGetQueue,
    useRemoveFromQueue,
    useReorderQueue,
    useReplaceQueue,
    useSetQueueCurrentSong,
} from '../client/playlists';
import type {GetPlaylistSong, ListSongsItem} from '../model';
import {AddToQueuePosition} from '../model';
import {usePlaybackActions} from '../stores/playback-store';

export type PlayableItem = GetPlaylistSong | ListSongsItem;

function isPlaylistSong(song: PlayableItem): song is GetPlaylistSong {
    return 'order' in song;
}

function toPlaylistSong(song: PlayableItem, order: number): GetPlaylistSong {
    if (isPlaylistSong(song)) {
        return {...song, order};
    }
    return {
        ...song,
        order,
        addedAtPlaylist: new Date().toISOString(),
    } as GetPlaylistSong;
}

export function useQueue() {
    const {data, isLoading} = useGetQueue({});
    const queue = data?.data?.playlist?.songs ?? [];
    const currentSongId = data?.data?.playlist?.currentSongId;

    return {queue, currentSongId, isLoading};
}

export function useQueueMutations() {
    const queryClient = useQueryClient();
    const {setLoadingSong: setLoadingSongAction, clear: clearAction, incrementPlaybackKey} = usePlaybackActions(
        (s) => ({setLoadingSong: s.setLoadingSong, clear: s.clear, incrementPlaybackKey: s.incrementPlaybackKey})
    );

    const replaceQueue = useReplaceQueue({});
    const addToQueue = useAddToQueue({});
    const removeFromQueue = useRemoveFromQueue({});
    const reorderQueue = useReorderQueue({});
    const setCurrentSong = useSetQueueCurrentSong({});

    const setLoadingSong = useCallback((song: GetPlaylistSong) => {
        setLoadingSongAction(song, true);
    }, [setLoadingSongAction]);

    const play = useCallback((songs: PlayableItem[]) => {
        if (songs.length === 0) return;

        console.log(1);
        incrementPlaybackKey();

        const songIds = songs.map((s) => s.id);
        const firstSong = songs[0];
        const songsToAdd = songs.map((song, i) => toPlaylistSong(song, i));

        setLoadingSong(songsToAdd[0]);

        const optimisticQueue = songsToAdd;
        queryClient.setQueryData(getGetQueueQueryKey(), {
            data: {
                playlist: {
                    id: 0,
                    name: 'Queue',
                    type: 1,
                    currentSongId: firstSong.id,
                    songs: optimisticQueue,
                },
            },
            status: 200,
            headers: new Headers(),
        });

        replaceQueue.mutate({data: {songIds, currentSongId: firstSong.id}});
    }, [replaceQueue, queryClient, setLoadingSong, incrementPlaybackKey]);

    const playNext = useCallback((songs: PlayableItem[]) => {
        if (songs.length === 0) return;
        const songIds = songs.map((s) => s.id);
        addToQueue.mutate({data: {songIds, position: AddToQueuePosition.Next}});
    }, [addToQueue]);

    const playLast = useCallback((songs: PlayableItem[]) => {
        if (songs.length === 0) return;
        const songIds = songs.map((s) => s.id);
        addToQueue.mutate({data: {songIds, position: AddToQueuePosition.Last}});
    }, [addToQueue]);

    const removeByIndices = useCallback((indices: number[], currentSongId: number | null | undefined) => {
        const queueData = queryClient.getQueryData(getGetQueueQueryKey()) as
            | { data: { playlist: { songs: GetPlaylistSong[] } } }
            | undefined;
        const queue = queueData?.data?.playlist?.songs ?? [];
        const songIds = indices.map((i) => queue[i]?.id).filter((id): id is number => id !== undefined);

        if (songIds.length === 0) return;

        const currentSongOrder = queue.find((s) => s.id === currentSongId)?.order ?? -1;
        const isCurrentSongRemoved = currentSongOrder >= 0 && indices.includes(currentSongOrder);

        if (isCurrentSongRemoved) {
            const remainingQueue = queue.filter((_, i) => !indices.includes(i));
            if (remainingQueue.length > 0) {
                const nextIndex = Math.min(currentSongOrder, remainingQueue.length - 1);
                const nextSong = remainingQueue[nextIndex];
                setLoadingSong(nextSong);
                setCurrentSong.mutate({data: {currentSongId: nextSong.id}});
            } else {
                clearAction();
                setCurrentSong.mutate({data: {currentSongId: null}});
            }
        }

        removeFromQueue.mutate({data: {songIds}});
    }, [removeFromQueue, setCurrentSong, clearAction, setLoadingSong, queryClient]);

    const reorder = useCallback((fromIndex: number, toIndex: number) => {
        reorderQueue.mutate({data: {reorders: [{fromIndex, toIndex}]}});
    }, [reorderQueue]);

    const reorderBatch = useCallback((reorders: { fromIndex: number; toIndex: number }[]) => {
        reorderQueue.mutate({data: {reorders}});
    }, [reorderQueue]);

    const updateCurrentSong = useCallback((songId: number | null) => {
        setCurrentSong.mutate({data: {currentSongId: songId}});
    }, [setCurrentSong]);

    return {
        play,
        playNext,
        playLast,
        removeByIndices,
        reorder,
        reorderBatch,
        updateCurrentSong,
    };
}
