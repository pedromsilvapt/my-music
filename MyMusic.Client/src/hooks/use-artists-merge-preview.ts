import {useQuery} from '@tanstack/react-query';
import {getListArtistsQueryKey, previewArtistsMerge} from '../client/artists';
import {throwOnProblem} from '../utils/api-problem';

/**
 * What merging the given artists into the target would change. Wraps the Orval-generated previewArtistsMerge as
 * a query: the endpoint is a POST (a selection of any size fits in its body), which Orval only exposes as a
 * mutation. Fails with an ApiProblemError, carrying the server's reason, when the artists cannot be merged.
 */
export function useArtistsMergePreview(targetId: number | undefined, sourceIds: number[]) {
    return useQuery({
        queryKey: [...getListArtistsQueryKey(), 'merge-preview', targetId, sourceIds],
        queryFn: async ({signal}) =>
            throwOnProblem(await previewArtistsMerge({targetId: targetId!, sourceIds}, {signal})).data,
        enabled: targetId !== undefined && sourceIds.length > 0,
        // The server's answer is final: asking again would only delay showing why it refused
        retry: false,
        // Always asked again: the songs may have changed since the dialog was last open
        gcTime: 0,
    });
}
