import {useListSharers} from "../client/song-sharing";

export function useSharers() {
    const query = useListSharers({
        query: {
            select: (response) => response.data,
        },
    });

    return {
        sharers: query.data?.sharers ?? [],
        isLoading: query.isLoading,
        isError: query.isError,
    };
}