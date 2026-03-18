import {useMutation, useQueryClient} from "@tanstack/react-query";

interface ClearAllTasksResponse {
    tasksDeleted: number;
    metadataDeleted: number;
}

async function clearAllTasksAndMetadata(): Promise<ClearAllTasksResponse> {
    const response = await fetch('/api/metadata-fetch/clear-all', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
    });

    if (!response.ok) {
        throw new Error('Failed to clear tasks and metadata');
    }

    return response.json();
}

export function useClearAllTasks() {
    const queryClient = useQueryClient();

    return useMutation<ClearAllTasksResponse, Error, void>({
        mutationFn: clearAllTasksAndMetadata,
        onSettled: () => {
            // Invalidate all metadata-fetch related queries
            queryClient.invalidateQueries({queryKey: ['metadata-fetch']});
        },
    });
}
