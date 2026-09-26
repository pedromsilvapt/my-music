import {useEffect} from 'react';
import {usePlaybackActions, usePlaybackStore} from '../stores/playback-store';
import {useQueue} from './use-queue';
import {useQueueManagerStore} from '../stores/queue-manager-store';
import {useUpdateCurrentUser} from '../client/users';
import {resolveQueueInitializerAction} from './player-queue-initializer-actions';

export function usePlayerQueueInitializer() {
    const {queue, currentSongId, isLoading, isFetching} = useQueue();
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
        const action = resolveQueueInitializerAction({
            isLoading,
            isFetching,
            queue,
            currentSongId,
            currentType,
            currentQueueId,
            visibleQueueId,
        });

        if (action.type === 'clear') {
            clear();
            setCurrentQueueId(null);
            // Clear currentQueueId on server
            updateCurrentUserMutation.mutate({data: {currentQueueId: null}});
        } else if (action.type === 'load') {
            setLoadingSong(action.song, false);
            setCurrentQueueId(visibleQueueId);
        }
    }, [isLoading, isFetching, currentType, currentSongId, queue, setLoadingSong, clear, currentQueueId, visibleQueueId, setCurrentQueueId, updateCurrentUserMutation]);
}
