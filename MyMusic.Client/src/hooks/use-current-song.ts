import type {GetPlaylistSong} from '../model';
import {usePlaybackStore} from '../stores/playback-store';
import {useQueue} from './use-queue';

export function useCurrentSong(): GetPlaylistSong | null {
    const song = usePlaybackStore((state) => {
        const current = state.current;
        if (current.type === 'LOADING' || current.type === 'LOADED') {
            return current.song;
        }
        return null;
    });
    const {queue, currentSongId} = useQueue();

    if (song) return song;

    if (currentSongId != null) {
        return queue.find((s) => s.id === currentSongId) ?? null;
    }

    return null;
}

export function useCurrentSongId(): number | null | undefined {
    const songId = usePlaybackStore((state) => {
        const current = state.current;
        if (current.type === 'LOADING' || current.type === 'LOADED') {
            return current.song.id;
        }
        return null;
    });
    const {currentSongId} = useQueue();
    return songId ?? currentSongId;
}

export function useIsPlayerActive(): boolean {
    return usePlaybackStore((state) => state.current.type !== 'EMPTY');
}
