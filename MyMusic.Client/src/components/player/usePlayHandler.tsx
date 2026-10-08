import {useCallback, useRef} from 'react';
import {usePlayerNavigation} from '../../hooks/use-player-navigation';
import type {PlayableItem} from '../../hooks/use-queue';
import {useQueueMutations} from '../../hooks/use-queue';
import {usePlayInNewQueue} from '../../hooks/use-play-in-new-queue';
import type {QueueContext} from '../../utils/queue-name-generator';
import {usePlaybackActions} from '../../stores/playback-store';
import {useQueueManagerStore} from '../../stores/queue-manager-store';
import {useQueryClient} from '@tanstack/react-query';
import {
    getGetPlaylistQueryKey,
    getGetQueueQueryKey,
    useSetQueueCurrentSongById,
    type getPlaylistResponse,
    type getQueueResponse,
} from '../../client/playlists';
import {withCurrentSong} from '../../hooks/queue-utils';
import type {GetPlaylistSongItem} from '../../model';

export type PlayHandler = (
    rows: PlayableItem[],
    ev: React.MouseEvent<Element, MouseEvent>,
    context?: QueueContext,
    allItems?: PlayableItem[]
) => void;

export interface UsePlayHandlerOptions {
    visibleQueueId: number | null;
    currentQueueId: number | null;
}

// useSetQueueCurrentSongById returns a new object every render.
// Wrap in useRef to avoid unstable references in dependency arrays.
export function usePlayHandler(
    nowPlaying: boolean = false,
    options?: UsePlayHandlerOptions
): PlayHandler {
    const {playNext, playLast} = useQueueMutations();
    const playInNewQueue = usePlayInNewQueue();
    const {goTo} = usePlayerNavigation();
    const {setLoadingSong} = usePlaybackActions(s => ({setLoadingSong: s.setLoadingSong}));
    const setQueueCurrentSongByIdRef = useRef(useSetQueueCurrentSongById({}));
    const setCurrentQueueId = useQueueManagerStore((s) => s.setCurrentQueueId);
    const queryClient = useQueryClient();

    const {visibleQueueId, currentQueueId} = options ?? {visibleQueueId: null, currentQueueId: null};

    // Switch to the different queue and play the song in a single atomic request
    const switchQueueAndPlay = useCallback((
        queueId: number,
        clickedSong: GetPlaylistSongItem
    ) => {
        const queueKey = getGetQueueQueryKey();
        const previousQueueData = queryClient.getQueryData<getQueueResponse>(queueKey);
        const previousCurrentQueueId = currentQueueId;
        const visibleQueueData = queryClient.getQueryData<getPlaylistResponse>(getGetPlaylistQueryKey(queueId));

        // Once it becomes the current queue, the list reads from the playing queue cache, which still
        // holds the old queue until the request completes. Seed it with the already loaded queue so
        // the list doesn't jump back to the old queue in the meantime.
        void queryClient.cancelQueries({queryKey: queueKey});
        if (visibleQueueData) {
            queryClient.setQueryData(queueKey, withCurrentSong(visibleQueueData, clickedSong.id));
        }

        setCurrentQueueId(queueId);
        setLoadingSong(clickedSong, true);
        setQueueCurrentSongByIdRef.current.mutate({
            id: queueId,
            data: {currentSongId: clickedSong.id}
        }, {
            onSuccess: (response) => {
                queryClient.setQueryData(queueKey, response);
                queryClient.invalidateQueries({queryKey: queueKey});
            },
            onError: () => {
                queryClient.setQueryData(queueKey, previousQueueData);
                setCurrentQueueId(previousCurrentQueueId);
            },
        });
    }, [queryClient, currentQueueId, setCurrentQueueId, setLoadingSong]);

    return useCallback((
        rows: PlayableItem[],
        ev: React.MouseEvent<Element, MouseEvent>,
        context?: QueueContext,
        allItems?: PlayableItem[]
    ) => {
        ev.stopPropagation();

        if (nowPlaying && rows.length === 1 && 'order' in rows[0]) {
            const clickedSong = rows[0] as GetPlaylistSongItem;

            if (visibleQueueId && visibleQueueId !== currentQueueId) {
                switchQueueAndPlay(visibleQueueId, clickedSong);
            } else {
                // Already viewing the current queue - use normal navigation
                // order is 1-indexed, goTo expects 0-indexed array position
                goTo(rows[0].order - 1);
            }
        } else if (ev.ctrlKey) {
            playLast(rows);
        } else if (ev.shiftKey) {
            playNext(rows);
        } else {
            playInNewQueue(rows, context, allItems);
        }
    }, [nowPlaying, visibleQueueId, currentQueueId, switchQueueAndPlay, goTo, playInNewQueue, playNext, playLast]);
}
