import {useGetPlaylist, useGetQueue} from '../client/playlists';
import {useQueueManagerStore} from '../stores/queue-manager-store';
import {useShallow} from 'zustand/react/shallow';

export interface VisibleQueueResult {
    queue: import('../model').GetPlaylistSongItem[];
    currentSongId: number | null;
    isLoading: boolean;
    queueId: number | null;
}

export function useVisibleQueue(): VisibleQueueResult {
    const {visibleQueueId, currentQueueId} = useQueueManagerStore(
        useShallow((state) => ({
            visibleQueueId: state.visibleQueueId,
            currentQueueId: state.currentQueueId,
        }))
    );

    const currentQueueQuery = useGetQueue({});

    const shouldFetchVisibleQueue = visibleQueueId !== null && visibleQueueId !== currentQueueId;
    const visibleQueueQuery = useGetPlaylist(
        shouldFetchVisibleQueue ? visibleQueueId : 0,
        {query: {enabled: shouldFetchVisibleQueue}}
    );

    const viewingCurrentQueue = visibleQueueId === null || visibleQueueId === currentQueueId;

    if (viewingCurrentQueue) {
        const queue = currentQueueQuery.data?.data?.playlist?.songs ?? [];
        const currentSongId = currentQueueQuery.data?.data?.playlist?.currentSongId ?? null;
        const isLoading = currentQueueQuery.isLoading;
        const queueId = currentQueueId;
        return {queue, currentSongId, isLoading, queueId};
    }

    const playlist = visibleQueueQuery.data?.data?.playlist;
    const queue = playlist?.songs ?? [];
    const currentSongId = playlist?.currentSongId ?? null;
    const isLoading = visibleQueueQuery.isLoading;
    const queueId = visibleQueueId;

    return {queue, currentSongId, isLoading, queueId};
}
