import { QueryClient, useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useRef } from 'react';
import {
    getGetQueueQueryKey,
    useAddToQueue,
    useBatchSetStopAfterPlayback,
    useGetQueue,
    useRemoveFromQueue,
    useReorderQueue,
    useReplaceQueue,
    useSetQueueCurrentSong,
    useShuffleQueue,
} from '../client/playlists';
import { useUpdateCurrentUser } from '../client/users';
import type { GetPlaylistItem, GetPlaylistSongItem } from '../model';
import { AddToQueuePosition } from '../model';
import { usePlaybackActions } from '../stores/playback-store';
import { useQueueManagerStore } from '../stores/queue-manager-store';
import {
    compactOrders,
    filterOutSongIds,
    playLastSongs,
    playNextSongs,
    reorderSongs,
    toPlaylistSong,
} from './queue-utils';
import type { PlayableItem } from './queue-utils';

export type { PlayableItem } from './queue-utils';

interface QueueQueryData {
    data: {
        playlist: GetPlaylistItem;
    };
}

interface QueueCache {
    queryKey: readonly unknown[];
    previousData: QueueQueryData | undefined;
    songs: GetPlaylistSongItem[];
    currentSongId: number | null | undefined;
}

function getQueueCache (queryClient: QueryClient): QueueCache {
    const queryKey = getGetQueueQueryKey();
    const previousData = queryClient.getQueryData<QueueQueryData>(queryKey);
    return {
        queryKey,
        previousData,
        songs: previousData?.data?.playlist?.songs ?? [],
        currentSongId: previousData?.data?.playlist?.currentSongId,
    };
}

export function useQueue () {
    const { data, isLoading } = useGetQueue({});
    const queue = data?.data?.playlist?.songs ?? [];
    const currentSongId = data?.data?.playlist?.currentSongId;
    const queueId = data?.data?.playlist?.id;

    return { queue, currentSongId, isLoading, queueId };
}

export function useQueueMutations () {
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
    const batchSetStopAfterPlaybackRef = useRef(useBatchSetStopAfterPlayback({}));

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
                data: { currentQueueId: visibleQueueId },
            });

            replaceQueueRef.current.mutate({ data: { songIds, currentSongId: firstSong.id } });
        },
        [queryClient, setLoadingSong, incrementPlaybackKey, setCurrentQueueId, visibleQueueId]
    );

    const playNext = useCallback(
        (songs: PlayableItem[]) => {
            if (songs.length === 0) return;

            const songIds = songs.map((s) => s.id);
            const { queryKey, previousData, songs: currentQueue, currentSongId } = getQueueCache(queryClient);

            const optimisticQueue = playNextSongs(currentQueue, songs, currentSongId);

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            addToQueueRef.current.mutate(
                { data: { songIds, position: AddToQueuePosition.Next } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
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
            const { queryKey, previousData, songs: currentQueue } = getQueueCache(queryClient);

            const optimisticQueue = playLastSongs(currentQueue, songs);

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            addToQueueRef.current.mutate(
                { data: { songIds, position: AddToQueuePosition.Last } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
                    },
                }
            );
        },
        [queryClient]
    );

    const removeBySongIds = useCallback(
        (songIds: number[], currentSongId: number | null | undefined) => {
            const { queryKey, previousData, songs: queue } = getQueueCache(queryClient);

            const songIdsToRemove = new Set(songIds);

            if (songIdsToRemove.size === 0) return;

            const isCurrentSongRemoved =
                currentSongId !== null && currentSongId !== undefined && songIdsToRemove.has(currentSongId);

            const optimisticQueue = filterOutSongIds(queue, songIdsToRemove);

            if (isCurrentSongRemoved) {
                if (optimisticQueue.length > 0) {
                    const currentIndex = queue.findIndex((s) => s.id === currentSongId);
                    const nextIndex = Math.min(currentIndex, optimisticQueue.length - 1);
                    const nextSong = optimisticQueue[nextIndex];
                    setLoadingSong(nextSong);
                    setCurrentSongRef.current.mutate({ data: { currentSongId: nextSong.id } });
                } else {
                    clearAction();
                    setCurrentSongRef.current.mutate({ data: { currentSongId: null } });
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
                { data: { songIds: Array.from(songIdsToRemove) } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
                    },
                }
            );
        },
        [clearAction, setLoadingSong, queryClient]
    );

    const removeByIndices = useCallback(
        (indices: number[], currentSongId: number | null | undefined) => {
            const { songs: queue } = getQueueCache(queryClient);

            const songsToRemove = indices.map((i) => queue[i]).filter((s): s is GetPlaylistSongItem => !!s);
            const songIds = songsToRemove.map((s) => s.id);

            removeBySongIds(songIds, currentSongId);
        },
        [removeBySongIds, queryClient]
    );

    const reorder = useCallback(
        (fromIndex: number, toIndex: number) => {
            const { queryKey, previousData, songs: queue } = getQueueCache(queryClient);

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
                { data: { reorders: [{ fromIndex, toIndex }] } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
                    },
                }
            );
        },
        [queryClient]
    );

    const reorderBatch = useCallback(
        (reorders: { fromIndex: number; toIndex: number; }[]) => {
            const { queryKey, previousData, songs } = getQueueCache(queryClient);
            let queue = songs;

            const validReorders: { fromIndex: number; toIndex: number; }[] = [];

            for (const { fromIndex, toIndex } of reorders) {
                if (fromIndex >= 0 && fromIndex < queue.length && toIndex >= 0 && toIndex < queue.length) {
                    queue = reorderSongs(queue, fromIndex, toIndex);
                    validReorders.push({ fromIndex, toIndex });
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
                { data: { reorders: validReorders } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
                    },
                }
            );
        },
        [queryClient]
    );

    const updateCurrentSong = useCallback(
        (songId: number | null) => {
            setCurrentSongRef.current.mutate({ data: { currentSongId: songId } });
        },
        []
    );

    const shuffleByIndices = useCallback(
        (indices: number[]) => {
            if (indices.length < 2) return;

            const { queryKey, previousData, songs: queue } = getQueueCache(queryClient);

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
                { data: { indices: validIndices } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
                    },
                }
            );
        },
        [queryClient]
    );

    const toggleStopAfterPlayback = useCallback(
        (songIds: number[], stopAfterPlayback: boolean, queueId: number) => {
            const { queryKey, previousData, songs: queue } = getQueueCache(queryClient);

            const songIdSet = new Set(songIds);

            const optimisticQueue = queue.map((s) =>
                songIdSet.has(s.id) ? { ...s, stopAfterPlayback } : s
            );

            queryClient.setQueryData<QueueQueryData>(queryKey, {
                data: {
                    playlist: {
                        ...previousData!.data.playlist,
                        songs: optimisticQueue,
                    },
                },
            });

            batchSetStopAfterPlaybackRef.current.mutate(
                { data: { playlistId: queueId, songIds, stopAfterPlayback } },
                {
                    onError: () => {
                        if (previousData) {
                            queryClient.setQueryData<QueueQueryData>(queryKey, previousData);
                        }
                    },
                    onSettled: () => {
                        queryClient.invalidateQueries({ queryKey });
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
        removeBySongIds,
        removeByIndices,
        reorder,
        reorderBatch,
        updateCurrentSong,
        shuffleByIndices,
        toggleStopAfterPlayback,
    }), [play, playNext, playLast, removeBySongIds, removeByIndices, reorder, reorderBatch, updateCurrentSong, shuffleByIndices, toggleStopAfterPlayback]);
}
