import {useQueryClient} from '@tanstack/react-query';
import {useCallback, useMemo, useRef} from 'react';
import {
    getListQueuesQueryKey,
    useCreateQueue,
    useDeleteQueue,
    useRenameQueue,
    useListQueues,
} from '../client/playlists';
import {useUpdateCurrentUser, useGetCurrentUser} from '../client/users';
import {useQueueManagerStore} from '../stores/queue-manager-store';
import type {ListQueueItem} from '../model';

export function useQueues() {
    const {data, isLoading} = useListQueues({});
    const queues = data?.data?.queues ?? [];
    return {queues, isLoading};
}

export function useVisibleQueueId() {
    return useQueueManagerStore((s) => s.visibleQueueId);
}

export function useCurrentQueueId() {
    const {data} = useGetCurrentUser({});
    const storeCurrentQueueId = useQueueManagerStore((s) => s.currentQueueId);
    
    // Prefer store value, fall back to server value
    return storeCurrentQueueId ?? data?.data?.user?.currentQueueId ?? null;
}

export function useQueuesMutations() {
    const queryClient = useQueryClient();
    const setVisibleQueueId = useQueueManagerStore((s) => s.setVisibleQueueId);
    const setCurrentQueueId = useQueueManagerStore((s) => s.setCurrentQueueId);

    // Use refs for mutations to avoid unstable references in dependency arrays
    const createQueueMutationRef = useRef(useCreateQueue({}));
    const deleteQueueMutationRef = useRef(useDeleteQueue({}));
    const renameQueueMutationRef = useRef(useRenameQueue({}));
    const updateCurrentUserMutationRef = useRef(useUpdateCurrentUser({}));

    const createQueue = useCallback(async (
        songIds: number[],
        options?: { name?: string; currentSongId?: number }
    ): Promise<number | null> => {
        const currentSongId = options?.currentSongId ?? (songIds.length > 0 ? songIds[0] : undefined);
        
        const result = await createQueueMutationRef.current.mutateAsync({
            data: {
                songIds,
                name: options?.name,
                currentSongId,
            },
        });

        const queueId = result.data.queue.id;
        setVisibleQueueId(queueId);
        setCurrentQueueId(queueId);
        
        // Update user's current queue on server
        await updateCurrentUserMutationRef.current.mutateAsync({
            data: {currentQueueId: queueId},
        });

        queryClient.invalidateQueries({queryKey: getListQueuesQueryKey()});
        
        return queueId;
    }, [setVisibleQueueId, setCurrentQueueId, queryClient]);

    const deleteQueue = useCallback(async (queueId: number): Promise<void> => {
        await deleteQueueMutationRef.current.mutateAsync({id: queueId});
        queryClient.invalidateQueries({queryKey: getListQueuesQueryKey()});
    }, [queryClient]);

    const renameQueue = useCallback(async (queueId: number, name: string): Promise<void> => {
        await renameQueueMutationRef.current.mutateAsync({id: queueId, data: {name}});
        queryClient.invalidateQueries({queryKey: getListQueuesQueryKey()});
    }, [queryClient]);

    // View a queue without affecting playback
    const viewQueue = useCallback((queueId: number): void => {
        setVisibleQueueId(queueId);
    }, [setVisibleQueueId]);

    // Set current queue (playing queue) - persists to server
    const playFromQueue = useCallback(async (queueId: number): Promise<void> => {
        setCurrentQueueId(queueId);
        await updateCurrentUserMutationRef.current.mutateAsync({
            data: {currentQueueId: queueId},
        });
    }, [setCurrentQueueId]);

    return useMemo(() => ({
        createQueue,
        deleteQueue,
        renameQueue,
        viewQueue,
        playFromQueue,
        isCreating: createQueueMutationRef.current.isPending,
        isDeleting: deleteQueueMutationRef.current.isPending,
        isRenaming: renameQueueMutationRef.current.isPending,
    }), [createQueue, deleteQueue, renameQueue, viewQueue, playFromQueue]);
}

export function useQueueList(): {
    queues: ListQueueItem[];
    visibleQueueId: number | null;
    currentQueueId: number | null;
    isLoading: boolean;
} {
    const {queues, isLoading} = useQueues();
    const visibleQueueId = useVisibleQueueId();
    const currentQueueId = useCurrentQueueId();
    
    return {queues, visibleQueueId, currentQueueId, isLoading};
}