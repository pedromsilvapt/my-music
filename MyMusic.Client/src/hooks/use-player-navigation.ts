import {useCallback} from 'react';
import {useSetQueueCurrentSong} from '../client/playlists';
import type {GetPlaylistSong} from '../model';
import {usePlaybackStoreApi} from '../stores/playback-store';
import {useQueue} from './use-queue';

export function usePlayerNavigation() {
    const {queue, currentSongId} = useQueue();
    const playbackStore = usePlaybackStoreApi();
    const setCurrentSong = useSetQueueCurrentSong({});

    const currentSong = queue.find((s) => s.id === currentSongId);
    const currentIndex = currentSong?.order ?? -1;
    const hasNext = currentIndex >= 0 && currentIndex < queue.length - 1;
    const hasPrevious = currentIndex > 0;

    const navigateToSong = useCallback((song: GetPlaylistSong | undefined) => {
        if (!song) return;

        playbackStore.getState().setLoadingSong(song, true);
        setCurrentSong.mutate({data: {currentSongId: song.id}});
    }, [playbackStore, setCurrentSong]);

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

    return {
        goForward,
        goBackward,
        goTo,
        hasNext,
        hasPrevious,
        currentIndex,
    };
}
