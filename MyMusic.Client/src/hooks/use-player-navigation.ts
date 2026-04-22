import {useCallback, useMemo, useRef} from 'react';
import {useSetQueueCurrentSong} from '../client/playlists';
import type {GetPlaylistSongItem} from '../model';
import {usePlaybackActions} from '../stores/playback-store';
import {useQueue, useQueueMutations} from './use-queue';

export interface GoForwardResult {
    skippedSongIds: number[];
    allRemainingSkipped: boolean;
}

export function usePlayerNavigation() {
    const {queue, currentSongId, queueId} = useQueue();
    const {clearSkipNextPlayback} = useQueueMutations();
    const {setLoadingSong} = usePlaybackActions(s => ({setLoadingSong: s.setLoadingSong}));
    const setCurrentSongRef = useRef(useSetQueueCurrentSong({}));

    const currentIndex = queue.findIndex((s) => s.id === currentSongId);
    const hasNext = currentIndex >= 0 && currentIndex < queue.length - 1;
    const hasPrevious = currentIndex > 0;

    const navigateToSong = useCallback((song: GetPlaylistSongItem | undefined) => {
        if (!song) return;

        setLoadingSong(song, true);
        setCurrentSongRef.current.mutate({data: {currentSongId: song.id}});

        if (song.skipNextPlayback && queueId != null) {
            clearSkipNextPlayback([song.id], queueId);
        }
    }, [setLoadingSong, queueId, clearSkipNextPlayback]);

    const goForward = useCallback((): GoForwardResult | null => {
        if (!hasNext) return null;

        const skippedSongIds: number[] = [];
        let nextSong: GetPlaylistSongItem | undefined;

        for (let i = currentIndex + 1; i < queue.length; i++) {
            const candidate = queue[i];
            if (candidate.skipNextPlayback) {
                skippedSongIds.push(candidate.id);
            } else {
                nextSong = candidate;
                break;
            }
        }

        const allRemainingSkipped = !nextSong;

        if (nextSong) {
            navigateToSong(nextSong);
        }

        if (skippedSongIds.length > 0 && queueId != null) {
            clearSkipNextPlayback(skippedSongIds, queueId);
        }

        return {skippedSongIds, allRemainingSkipped};
    }, [hasNext, queue, currentIndex, navigateToSong, queueId, clearSkipNextPlayback]);

    const goBackward = useCallback(() => {
        if (!hasPrevious) return;
        navigateToSong(queue[currentIndex - 1]);
    }, [hasPrevious, queue, currentIndex, navigateToSong]);

    const goTo = useCallback((index: number) => {
        if (index < 0 || index >= queue.length) return;
        navigateToSong(queue[index]);
    }, [queue, navigateToSong]);

    return useMemo(() => ({
        goForward,
        goBackward,
        goTo,
        hasNext,
        hasPrevious,
        currentIndex,
    }), [goForward, goBackward, goTo, hasNext, hasPrevious, currentIndex]);
}
