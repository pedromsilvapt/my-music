import {useCallback, useRef} from 'react';
import {usePlayerNavigation} from '../../hooks/use-player-navigation';
import type {PlayableItem} from '../../hooks/use-queue';
import {useQueueMutations} from '../../hooks/use-queue';
import {useQueuesMutations} from '../../hooks/use-queues';
import {generateQueueName, type QueueContext} from '../../utils/queue-name-generator';
import {usePlaybackActions} from '../../stores/playback-store';
import {useQueueManagerStore} from '../../stores/queue-manager-store';
import {useQueryClient} from '@tanstack/react-query';
import {getGetQueueQueryKey, useSetQueueCurrentSongById} from '../../client/playlists';

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
    const {createQueue} = useQueuesMutations();
    const {goTo} = usePlayerNavigation();
    const {incrementPlaybackKey, setLoadingSong} = usePlaybackActions(s => ({
        incrementPlaybackKey: s.incrementPlaybackKey,
        setLoadingSong: s.setLoadingSong,
    }));
    const setQueueCurrentSongByIdRef = useRef(useSetQueueCurrentSongById({}));
    const setCurrentQueueId = useQueueManagerStore((s) => s.setCurrentQueueId);
    const queryClient = useQueryClient();

    const {visibleQueueId, currentQueueId} = options ?? {visibleQueueId: null, currentQueueId: null};

    const playAndCreateQueue = useCallback(async (
        rows: PlayableItem[],
        context?: QueueContext,
        allItems?: PlayableItem[]
    ) => {
        if (rows.length === 0) return;

        incrementPlaybackKey();

        const queueItems = allItems ?? rows;
        const clickedSong = rows[0];
        const songIds = queueItems.map((s) => s.id);
        const queueContext = context ?? {type: 'songs' as const};
        const name = generateQueueName(queueContext);

        const queueId = await createQueue(songIds, {name, currentSongId: clickedSong.id});

        if (queueId === null) return;

        const clickedIndex = queueItems.findIndex(s => s.id === clickedSong.id);
        const songWithOrder: import('../../model').GetPlaylistSongItem = {
            ...clickedSong,
            order: clickedIndex + 1,
            addedAtPlaylist: new Date().toISOString(),
        } as import('../../model').GetPlaylistSongItem;
        setLoadingSong(songWithOrder, true);
    }, [createQueue, incrementPlaybackKey, setLoadingSong]);

    return useCallback((
        rows: PlayableItem[],
        ev: React.MouseEvent<Element, MouseEvent>,
        context?: QueueContext,
        allItems?: PlayableItem[]
    ) => {
        ev.stopPropagation();

        if (nowPlaying && rows.length === 1 && 'order' in rows[0]) {
            const clickedSong = rows[0] as PlayableItem & { order: number };

            if (visibleQueueId && visibleQueueId !== currentQueueId) {
                // Switch to the different queue and play the song in a single atomic request
                setCurrentQueueId(visibleQueueId);
                setLoadingSong(clickedSong, true);
                setQueueCurrentSongByIdRef.current.mutate({
                    id: visibleQueueId,
                    data: { currentSongId: clickedSong.id }
                }, {
                    onSuccess: () => {
                        queryClient.invalidateQueries({ queryKey: getGetQueueQueryKey() });
                    }
                });
            } else {
                // Already viewing the current queue - use normal navigation
                goTo(rows[0].order);
            }
        } else if (ev.ctrlKey) {
            playLast(rows);
        } else if (ev.shiftKey) {
            playNext(rows);
        } else {
            playAndCreateQueue(rows, context, allItems);
        }
    }, [nowPlaying, visibleQueueId, currentQueueId, setCurrentQueueId, queryClient, setLoadingSong, goTo, playAndCreateQueue, playNext, playLast]);
}
