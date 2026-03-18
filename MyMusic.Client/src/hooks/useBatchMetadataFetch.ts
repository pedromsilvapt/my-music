import {useMutation} from "@tanstack/react-query";
import {useQueryClient} from "@tanstack/react-query";

interface BatchMetadataFetchResponse {
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

async function triggerBatchMetadataFetch(): Promise<BatchMetadataFetchResponse> {
    const response = await fetch('/api/metadata-fetch/batch', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({}),
    });

    if (!response.ok) {
        throw new Error('Failed to trigger metadata fetch');
    }

    return response.json();
}

interface MutationContext {
    previousStatus: MetadataQueueStatusResponse | undefined;
}

export function useBatchMetadataFetch() {
    const queryClient = useQueryClient();

    return useMutation<BatchMetadataFetchResponse, Error, void, MutationContext>({
        mutationFn: triggerBatchMetadataFetch,
        onMutate: async () => {
            // Cancel any outgoing refetches to avoid overwriting our optimistic update
            await queryClient.cancelQueries({queryKey: ['metadata-fetch', 'queue-status']});

            // Snapshot the previous value
            const previousStatus = queryClient.getQueryData<MetadataQueueStatusResponse>(
                ['metadata-fetch', 'queue-status']
            );

            // Optimistically update to show that tasks are being queued
            if (previousStatus) {
                queryClient.setQueryData(
                    ['metadata-fetch', 'queue-status'],
                    {
                        ...previousStatus,
                        queued: previousStatus.queued + 1, // Show at least 1 task is being queued
                        total: previousStatus.total + 1,
                    }
                );
            }

            // Return context with the previous value
            return {previousStatus};
        },
        onError: (_err, _variables, context) => {
            // If the mutation fails, roll back to the previous value
            if (context?.previousStatus) {
                queryClient.setQueryData(
                    ['metadata-fetch', 'queue-status'],
                    context.previousStatus
                );
            }
        },
        onSettled: () => {
            // Always refetch after error or success to ensure data is in sync
            queryClient.invalidateQueries({queryKey: ['metadata-fetch']});
        },
    });
}
