import {useCallback} from 'react';
import type {GetPlaylistSongItem} from '../model';
import {usePlaybackActions} from '../stores/playback-store';
import {generateQueueName, type QueueContext} from '../utils/queue-name-generator';
import type {PlayableItem} from './use-queue';
import {useQueuesMutations} from './use-queues';

export type PlayInNewQueue = (
    rows: PlayableItem[],
    context?: QueueContext,
    allItems?: PlayableItem[]
) => Promise<void>;

// Plays the first of `rows` in a newly created queue, which becomes the playing queue.
// The queue holds `allItems` when given (e.g. the whole list the song was played from), or `rows` otherwise.
export function usePlayInNewQueue(): PlayInNewQueue {
    const {createQueue} = useQueuesMutations();
    const {incrementPlaybackKey, setLoadingSong} = usePlaybackActions(s => ({
        incrementPlaybackKey: s.incrementPlaybackKey,
        setLoadingSong: s.setLoadingSong,
    }));

    return useCallback(async (
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
        const songWithOrder: GetPlaylistSongItem = {
            ...clickedSong,
            order: clickedIndex + 1,
            addedAtPlaylist: new Date().toISOString(),
        } as GetPlaylistSongItem;
        setLoadingSong(songWithOrder, true);
    }, [createQueue, incrementPlaybackKey, setLoadingSong]);
}
