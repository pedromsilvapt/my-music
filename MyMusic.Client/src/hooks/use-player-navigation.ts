import {useCallback, useMemo, useRef} from 'react';
import {useSetQueueCurrentSong} from '../client/playlists';
import type {GetPlaylistSongItem} from '../model';
import {usePlaybackActions} from '../stores/playback-store';
import {useQueue} from './use-queue';

// useSetQueueCurrentSong returns a new object every render.
// Wrap in useRef so callbacks don't need it in dependency arrays.
export function usePlayerNavigation() {
    const {queue, currentSongId} = useQueue();
    const {setLoadingSong} = usePlaybackActions(s => ({setLoadingSong: s.setLoadingSong}));
    const setCurrentSongRef = useRef(useSetQueueCurrentSong({}));

    const currentSong = queue.find((s) => s.id === currentSongId);
    const currentIndex = currentSong?.order ?? -1;
    const hasNext = currentIndex >= 0 && currentIndex < queue.length - 1;
    const hasPrevious = currentIndex > 0;

    const navigateToSong = useCallback((song: GetPlaylistSongItem | undefined) => {
        if (!song) return;

        setLoadingSong(song, true);
        setCurrentSongRef.current.mutate({data: {currentSongId: song.id}});
    }, [setLoadingSong]);

    const goForward = useCallback(() => {
        if (!hasNext) return;
        const nextSong = queue.find((s) => s.order === currentIndex + 1);
        navigateToSong(nextSong);
    }, [hasNext, queue, currentIndex, navigateToSong]);

    const goBackward = useCallback(() => {
        if (!hasPrevious) return;
        const prevSong = queue.find((s) => s.order === currentIndex - 1);
        navigateToSong(prevSong);
    }, [hasPrevious, queue, currentIndex, navigateToSong]);

    const goTo = useCallback((index: number) => {
        const song = queue.find((s) => s.order === index);
        navigateToSong(song);
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
