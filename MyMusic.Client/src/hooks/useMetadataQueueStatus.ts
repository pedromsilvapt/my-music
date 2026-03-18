import {useQuery} from "@tanstack/react-query";

interface MetadataQueueStatusResponse {
    queued: number;
    processing: number;
    completed: number;
    failed: number;
    total: number;
    estimatedCompletion?: string;
}

async function fetchQueueStatus(): Promise<MetadataQueueStatusResponse> {
    const response = await fetch("/api/metadata-fetch/queue-status");

    if (!response.ok) {
        throw new Error("Failed to fetch queue status");
    }

    return response.json();
}

export function useMetadataQueueStatus() {
    return useQuery({
        queryKey: ["metadata-fetch", "queue-status"],
        queryFn: fetchQueueStatus,
        refetchInterval: 5000, // Refetch every 5 seconds for live updates
    });
}
