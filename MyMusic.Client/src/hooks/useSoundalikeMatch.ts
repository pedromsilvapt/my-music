import {useQuery} from "@tanstack/react-query";
import {matchSoundalikes} from "../client/audits.ts";

/**
 * Scores how alike the given songs sound, whatever the score. The endpoint is a POST (the song ids travel in the
 * body), so the generated function is wrapped in a query rather than used as a mutation.
 */
export function useSoundalikeMatch(songIds: number[]) {
    return useQuery({
        queryKey: ['api', 'audits', 'soundalike', 'match', songIds],
        queryFn: ({signal}) => matchSoundalikes({songIds}, {signal}),
        // The score is only as good as the songs' current files: never serve a remembered one
        gcTime: 0,
        staleTime: 0,
    });
}
