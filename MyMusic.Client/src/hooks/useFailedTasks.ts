import {useQuery} from "@tanstack/react-query";

export type FailureReason = 
    | "None"
    | "ServiceUnavailable" 
    | "NoMetadataFound" 
    | "NetworkError" 
    | "SystemError" 
    | "Timeout";

interface FailedTaskDetail {
    taskId: number;
    songId: number;
    songTitle: string;
    reason: FailureReason;
    errorMessage: string;
    failedAt: string;
    retryCount: number;
}

async function fetchFailedTasks(): Promise<FailedTaskDetail[]> {
    const response = await fetch("/api/metadata-fetch/failed-tasks");

    if (!response.ok) {
        throw new Error("Failed to fetch failed tasks");
    }

    return response.json();
}

export function useFailedTasks() {
    return useQuery({
        queryKey: ["metadata-fetch", "failed-tasks"],
        queryFn: fetchFailedTasks,
        refetchInterval: 5000, // Refetch every 5 seconds for live updates
    });
}
