import {useListSharers} from "../client/playlist-sharing";

export function useSharers() {
    const query = useListSharers({
        query: {
            select: (response) => response.data,
        },
    });

    return {
        sharers: query.data?.sharers ?? [],
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
    };
}