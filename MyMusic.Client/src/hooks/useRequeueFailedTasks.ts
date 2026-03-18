import {useMutation} from "@tanstack/react-query";
import {useQueryClient} from "@tanstack/react-query";

interface RequeueFailedMetadataResponse {
    tasksCreated: number;
    message: string;
}

interface MetadataQueueStatusResponse {
    queued: number;
    processing: number;
    completed: number;
    failed: number;
    total: number;
    estimatedCompletion?: string;
}

async function requeueFailedTasks(): Promise<RequeueFailedMetadataResponse> {
    const response = await fetch('/api/metadata-fetch/requeue', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({}),
    });

    if (!response.ok) {
        throw new Error('Failed to requeue failed tasks');
    }

    return response.json();
}

interface MutationContext {
    previousStatus: MetadataQueueStatusResponse | undefined;
}

export function useRequeueFailedTasks() {
    const queryClient = useQueryClient();

    return useMutation<RequeueFailedMetadataResponse, Error, void, MutationContext>({
        mutationFn: requeueFailedTasks,
        onMutate: async () => {
            // Cancel any outgoing refetches
            await queryClient.cancelQueries({queryKey: ['metadata-fetch', 'queue-status']});

            // Snapshot the previous value
            const previousStatus = queryClient.getQueryData<MetadataQueueStatusResponse>(
                ['metadata-fetch', 'queue-status']
            );

            // Optimistically move failed tasks back to queued
            if (previousStatus && previousStatus.failed > 0) {
                queryClient.setQueryData(
                    ['metadata-fetch', 'queue-status'],
                    {
                        ...previousStatus,
                        queued: previousStatus.queued + previousStatus.failed,
                        failed: 0,
                    }
                );
            }

            return {previousStatus};
        },
        onError: (_err, _variables, context) => {
            // Roll back on error
            if (context?.previousStatus) {
                queryClient.setQueryData(
                    ['metadata-fetch', 'queue-status'],
                    context.previousStatus
                );
            }
        },
        onSettled: () => {
            // Always refetch after error or success
            queryClient.invalidateQueries({queryKey: ['metadata-fetch']});
        },
    });
}
