import {useMutation, useQueryClient} from "@tanstack/react-query";

interface ApplyMetadataRequest {
    appliedFields?: string[];
}

interface ApplyMetadataResponse {
    success: boolean;
    message: string;
}

async function applyMetadata(
    songId: number,
    request?: ApplyMetadataRequest
): Promise<ApplyMetadataResponse> {
    const response = await fetch(`/api/metadata-fetch/song/${songId}/apply`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
        },
        body: request ? JSON.stringify(request) : undefined,
    });

    if (!response.ok) {
        throw new Error("Failed to apply metadata");
    }

    return response.json();
}

export function useApplyMetadata() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({songId, request}: {songId: number; request?: ApplyMetadataRequest}) =>
            applyMetadata(songId, request),
        onSuccess: () => {
            // Invalidate metadata fetch queries to refresh the UI
            queryClient.invalidateQueries({queryKey: ["metadata-fetch", "song"]});
        },
    });
}
