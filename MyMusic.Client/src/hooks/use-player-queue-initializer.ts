import {useEffect} from 'react';
import {usePlaybackActions, usePlaybackStore} from '../stores/playback-store';
import {useQueue} from './use-queue';

export function usePlayerQueueInitializer() {
    const {queue, currentSongId, isLoading} = useQueue();
    const currentType = usePlaybackStore((s) => s.current.type);
    const {setLoadingSong, clear} = usePlaybackActions((s) => ({
        setLoadingSong: s.setLoadingSong,
        clear: s.clear,
    }));

    useEffect(() => {
        if (isLoading) return;

        // If queue is empty and player has a song loaded, clear it
        if (queue.length === 0 && currentType !== 'EMPTY') {
            clear();
            return;
        }

        // If player is empty and queue has a current song, load it
        if (currentType === 'EMPTY' && currentSongId != null) {
            const song = queue.find((s) => s.id === currentSongId);
            if (song) {
                setLoadingSong(song, false);
            }
        }
    }, [isLoading, currentType, currentSongId, queue, setLoadingSong, clear]);
}
