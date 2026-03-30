import {useEffect} from 'react';
import {usePlaybackActions, usePlaybackStore} from '../stores/playback-store';
import {useQueue} from './use-queue';
import {useQueueManagerStore} from '../stores/queue-manager-store';
import {useUpdateCurrentUser} from '../client/users';

export function usePlayerQueueInitializer() {
    const {queue, currentSongId, isLoading} = useQueue();
    const currentType = usePlaybackStore((s) => s.current.type);
    const currentQueueId = useQueueManagerStore((s) => s.currentQueueId);
    const visibleQueueId = useQueueManagerStore((s) => s.visibleQueueId);
    const {setLoadingSong, clear} = usePlaybackActions((s) => ({
        setLoadingSong: s.setLoadingSong,
        clear: s.clear,
    }));
    const setCurrentQueueId = useQueueManagerStore((s) => s.setCurrentQueueId);
    const updateCurrentUserMutation = useUpdateCurrentUser({});

    useEffect(() => {
        if (isLoading) return;

        // If viewing a different queue than what's playing, don't interfere with player
        if (currentQueueId !== null && currentQueueId !== visibleQueueId) {
            return;
        }

        // If queue is empty and player has a song loaded, clear it
        // Only clear if we're viewing the queue that's supposed to be playing
        if (queue.length === 0 && currentType !== 'EMPTY' && currentQueueId === visibleQueueId) {
            clear();
            setCurrentQueueId(null);
            // Clear currentQueueId on server
            updateCurrentUserMutation.mutate({data: {currentQueueId: null}});
            return;
        }

        // If player is empty and queue has a current song, load it
        if (currentType === 'EMPTY' && currentSongId != null) {
            const song = queue.find((s) => s.id === currentSongId);
            if (song) {
                setLoadingSong(song, false);
                setCurrentQueueId(visibleQueueId);
            }
        }
    }, [isLoading, currentType, currentSongId, queue, setLoadingSong, clear, currentQueueId, visibleQueueId, setCurrentQueueId, updateCurrentUserMutation]);
}
