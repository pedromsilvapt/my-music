import {useMutation, useQueryClient} from "@tanstack/react-query";
import {usePostMetadataFetchClearAll} from "../client/metadata-fetch";

export function useClearAllTasks() {
    const queryClient = useQueryClient();
    const mutation = usePostMetadataFetchClearAll({});

    return useMutation({
        ...mutation,
        onSettled: () => {
            // Invalidate all metadata-fetch related queries
            queryClient.invalidateQueries({queryKey: ['metadata-fetch']});
        },
    }, queryClient);
}