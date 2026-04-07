import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
    getResolveSoundalikesUrl,
    getGetSoundalikeDuplicatesQueryKey,
    getListAuditRulesQueryKey,
} from "../client/audits.ts";
import type {ResolveSoundalikesRequest} from "../model/resolveSoundalikesRequest.ts";
import type {ResolveSoundalikesResponse} from "../model/resolveSoundalikesResponse.ts";

async function resolveSoundalikes(
    request: ResolveSoundalikesRequest,
): Promise<ResolveSoundalikesResponse> {
    const res = await fetch(getResolveSoundalikesUrl(), {
        method: "POST",
        headers: {"Content-Type": "application/json"},
        body: JSON.stringify(request),
    });

    if (!res.ok) {
        throw new Error(`Failed to resolve duplicates (${res.status})`);
    }

    const body = [204, 205, 304].includes(res.status) ? null : await res.text();
    return body ? JSON.parse(body) : {resolvedCount: 0};
}

export function useResolveSoundalikes() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (request: ResolveSoundalikesRequest) =>
            resolveSoundalikes(request),
        onSuccess: () => {
            queryClient.invalidateQueries({queryKey: getGetSoundalikeDuplicatesQueryKey()});
            queryClient.invalidateQueries({queryKey: getListAuditRulesQueryKey()});
        },
    });
}
