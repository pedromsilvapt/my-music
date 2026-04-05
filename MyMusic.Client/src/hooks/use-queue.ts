import {useQueryClient} from '@tanstack/react-query';
import {useCallback, useMemo, useRef} from 'react';
import {
    getGetQueueQueryKey,
    useAddToQueue,
    useGetQueue,
    useRemoveFromQueue,
    useReorderQueue,
    useReplaceQueue,
    useSetQueueCurrentSong,
    useShuffleQueue,
} from '../client/playlists';
import {useUpdateCurrentUser} from '../client/users';
import type {GetPlaylistItem, GetPlaylistSongItem, ListSongItem} from '../model';
import {AddToQueuePosition} from '../model';
import {usePlaybackActions} from '../stores/playback-store';
import {useQueueManagerStore} from '../stores/queue-manager-store';

export type PlayableItem = GetPlaylistSongItem | ListSongItem;

interface QueueQueryData {
    data: {
        playlist: GetPlaylistItem;
    };
}

function isPlaylistSong(song: PlayableItem): song is GetPlaylistSongItem {
    return 'order' in song;
}

function toPlaylistSong(song: PlayableItem, order: number): GetPlaylistSongItem {
    if (isPlaylistSong(song)) {
        return {...song, order};
    }
    return {
        ...song,
        order,
        addedAtPlaylist: new Date().toISOString(),
    } as GetPlaylistSongItem;
}

function compactOrders(songs: GetPlaylistSongItem[]): GetPlaylistSongItem[] {
    return songs.map((song, index) => ({
        ...song,
        order: index + 1,
    }));
}

function insertAfterCurrent(
    songs: GetPlaylistSongItem[],
    newSongs: GetPlaylistSongItem[],
    currentSongId: number | null | undefined
): GetPlaylistSongItem[] {
    if (!currentSongId || songs.length === 0) {
        return compactOrders([...newSongs, ...songs]);
    }

    const currentIndex = songs.findIndex((s) => s.id === currentSongId);
    if (currentIndex < 0) {
        return compactOrders([...newSongs, ...songs]);
    }

    const beforeCurrent = songs.slice(0, currentIndex + 1);
    const afterCurrent = songs.slice(currentIndex + 1);
    return compactOrders([...beforeCurrent, ...newSongs, ...afterCurrent]);
}

function appendToEnd(songs: GetPlaylistSongItem[], newSongs: GetPlaylistSongItem[]): GetPlaylistSongItem[] {
    return compactOrders([...songs, ...newSongs]);
}

function removeBySongIds(songs: GetPlaylistSongItem[], songIdsToRemove: Set<number>): GetPlaylistSongItem[] {
    return compactOrders(songs.filter((s) => !songIdsToRemove.has(s.id)));
}

function reorderSongs(
    songs: GetPlaylistSongItem[],
    fromIndex: number,
    toIndex: number
): GetPlaylistSongItem[] {
    if (fromIndex < 0 || fromIndex >= songs.length || toIndex < 0 || toIndex >= songs.length) {
        return songs;
    }

    const result = [...songs];
    const [movedSong] = result.splice(fromIndex, 1);
    result.splice(toIndex, 0, movedSong);
    return compactOrders(result);
}

export function useQueue() {
    const {data, isLoading} = useGetQueue({});
    const queue = data?.data?.playlist?.songs ?? [];
    const currentSongId = data?.data?.playlist?.currentSongId;

    return {queue, currentSongId, isLoading};
}

export function useQueueMutations() {
    const queryClient = useQueryClient();
    const {
        setLoadingSong: setLoadingSongAction,
        clear: clearAction,
        incrementPlaybackKey,
    } = usePlaybackActions((s) => ({
        setLoadingSong: s.setLoadingSong,
        clear: s.clear,
        incrementPlaybackKey: s.incrementPlaybackKey,
    }));
    const setCurrentQueueId = useQueueManagerStore((s) => s.setCurrentQueueId);
    const visibleQueueId = useQueueManagerStore((s) => s.visibleQueueId);

    // Use refs for mutations to avoid unstable references in dependency arrays
    const updateCurrentUserMutationRef = useRef(useUpdateCurrentUser({}));
    const replaceQueueRef = useRef(useReplaceQueue({}));
    const addToQueueRef = useRef(useAddToQueue({}));
    const removeFromQueueRef = useRef(useRemoveFromQueue({}));
    const reorderQueueRef = useRef(useReorderQueue({}));
    const setCurrentSongRef = useRef(useSetQueueCurrentSong({}));
    const shuffleQueueRef = useRef(useShuffleQueue({}));

    const setLoadingSong = useCallback((song: GetPlaylistSongItem) => {
        setLoadingSongAction(song, true);
    }, [setLoadingSongAction]);

    const play = useCallback(
        async (songs: PlayableItem[]) => {
            if (songs.length === 0) return;

            incrementPlaybackKey();
            setCurrentQueueId(visibleQueueId);

            const songIds = songs.map((s) => s.id);
            const firstSong = songs[0];
            const songsToAdd = songs.map((song, i) => toPlaylistSong(song, i + 1));

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

            // Persist currentQueueId to server
            await updateCurrentUserMutationRef.current.mutateAsync({
                data: {currentQueueId: visibleQueueId},
            });

            replaceQueueRef.current.mutate({data: {songIds, currentSongId: firstSong.id}});
        },
        [queryClient, setLoadingSong, incrementPlaybackKey, setCurrentQueueId, visibleQueueId]
    );

    const playNext = useCallback(
        (songs: PlayableItem[]) => {
            if (songs.length === 0) return;

            const songIds = songs.map((s) => s.id);
            const queryKey = getGetQueueQueryKey();

            const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
            const currentQueue = previousData?.data?.playlist?.songs ?? [];
            const currentSongId = previousData?.data?.playlist?.currentSongId;

            const newSongs = songs.map((song, i) => toPlaylistSong(song, i + 1));
            const optimisticQueue = insertAfterCurrent(currentQueue, newSongs, currentSongId);

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            addToQueueRef.current.mutate(
                {data: {songIds, position: AddToQueuePosition.Next}},
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({queryKey});
                    },
                }
            );
        },
        [queryClient]
    );

    const playLast = useCallback(
        (songs: PlayableItem[]) => {
            if (songs.length === 0) return;

            const songIds = songs.map((s) => s.id);
            const queryKey = getGetQueueQueryKey();

            const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
            const currentQueue = previousData?.data?.playlist?.songs ?? [];

            const newSongs = songs.map((song, i) => toPlaylistSong(song, currentQueue.length + i + 1));
            const optimisticQueue = appendToEnd(currentQueue, newSongs);

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            addToQueueRef.current.mutate(
                {data: {songIds, position: AddToQueuePosition.Last}},
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({queryKey});
                    },
                }
            );
        },
        [queryClient]
    );

    const removeByIndices = useCallback(
        (indices: number[], currentSongId: number | null | undefined) => {
            const queryKey = getGetQueueQueryKey();
            const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
            const queue = previousData?.data?.playlist?.songs ?? [];

            const songsToRemove = indices.map((i) => queue[i]).filter((s): s is GetPlaylistSongItem => !!s);
            const songIdsToRemove = new Set(songsToRemove.map((s) => s.id));

            if (songIdsToRemove.size === 0) return;

            const isCurrentSongRemoved =
                currentSongId !== null && currentSongId !== undefined && songIdsToRemove.has(currentSongId);

            const optimisticQueue = removeBySongIds(queue, songIdsToRemove);

            if (isCurrentSongRemoved) {
                if (optimisticQueue.length > 0) {
                    const currentIndex = queue.findIndex((s) => s.id === currentSongId);
                    const nextIndex = Math.min(currentIndex, optimisticQueue.length - 1);
                    const nextSong = optimisticQueue[nextIndex];
                    setLoadingSong(nextSong);
                    setCurrentSongRef.current.mutate({data: {currentSongId: nextSong.id}});
                } else {
                    clearAction();
                    setCurrentSongRef.current.mutate({data: {currentSongId: null}});
                }
            }

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            removeFromQueueRef.current.mutate(
                {data: {songIds: Array.from(songIdsToRemove)}},
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({queryKey});
                    },
                }
            );
        },
        [clearAction, setLoadingSong, queryClient]
    );

    const reorder = useCallback(
        (fromIndex: number, toIndex: number) => {
            const queryKey = getGetQueueQueryKey();
            const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
            const queue = previousData?.data?.playlist?.songs ?? [];

            if (fromIndex < 0 || fromIndex >= queue.length || toIndex < 0 || toIndex >= queue.length) {
                return;
            }

            const optimisticQueue = reorderSongs(queue, fromIndex, toIndex);

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            reorderQueueRef.current.mutate(
                {data: {reorders: [{fromIndex, toIndex}]}},
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({queryKey});
                    },
                }
            );
        },
        [queryClient]
    );

    const reorderBatch = useCallback(
        (reorders: {fromIndex: number; toIndex: number}[]) => {
            const queryKey = getGetQueueQueryKey();
            const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
            let queue = previousData?.data?.playlist?.songs ?? [];

            const validReorders: {fromIndex: number; toIndex: number}[] = [];

            for (const {fromIndex, toIndex} of reorders) {
                if (fromIndex >= 0 && fromIndex < queue.length && toIndex >= 0 && toIndex < queue.length) {
                    queue = reorderSongs(queue, fromIndex, toIndex);
                    validReorders.push({fromIndex, toIndex});
                }
            }

            if (validReorders.length === 0) return;

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: queue,
                    },
                },
            });

            reorderQueueRef.current.mutate(
                {data: {reorders: validReorders}},
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({queryKey});
                    },
                }
            );
        },
        [queryClient]
    );

    const updateCurrentSong = useCallback(
        (songId: number | null) => {
            setCurrentSongRef.current.mutate({data: {currentSongId: songId}});
        },
        []
    );

    const shuffleByIndices = useCallback(
        (indices: number[]) => {
            if (indices.length < 2) return;

            const queryKey = getGetQueueQueryKey();
            const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
            const queue = previousData?.data?.playlist?.songs ?? [];

            const validIndices = indices.filter((i) => i >= 0 && i < queue.length);
            if (validIndices.length < 2) return;

            const songsAtIndices = validIndices.map((i) => queue[i]);
            const shuffledOrders = songsAtIndices.map((_, i) => i);
            for (let i = shuffledOrders.length - 1; i > 0; i--) {
                const j = Math.floor(Math.random() * (i + 1));
                [shuffledOrders[i], shuffledOrders[j]] = [shuffledOrders[j], shuffledOrders[i]];
            }

            const optimisticQueue = [...queue];
            for (let i = 0; i < validIndices.length; i++) {
                const shuffledIndex = shuffledOrders[i];
                optimisticQueue[validIndices[i]] = songsAtIndices[shuffledIndex];
            }

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: compactOrders(optimisticQueue),
                    },
                },
            });

            shuffleQueueRef.current.mutate(
                {data: {indices: validIndices}},
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({queryKey});
                    },
                }
            );
        },
        [queryClient]
    );

    return useMemo(() => ({
        play,
        playNext,
        playLast,
        removeByIndices,
        reorder,
        reorderBatch,
        updateCurrentSong,
        shuffleByIndices,
    }), [play, playNext, playLast, removeByIndices, reorder, reorderBatch, updateCurrentSong, shuffleByIndices]);
}