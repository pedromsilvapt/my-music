import {useQuery} from '@tanstack/react-query';
import {getArtistsUsage, getListArtistsQueryKey} from '../client/artists';

/**
 * How many songs deleting the given artists would change. Wraps the Orval-generated getArtistsUsage as a
 * query: the endpoint is a POST (a selection of any size fits in its body), which Orval only exposes
 * as a mutation.
 */
export function useArtistsUsage(artistIds: number[]) {
    return useQuery({
        queryKey: [...getListArtistsQueryKey(), 'usage', artistIds],
        queryFn: ({signal}) => getArtistsUsage({artistIds}, {signal}),
        select: response => response.status < 400 ? response.data : undefined,
        // Always asked again: the songs may have changed since the dialog was last open
        gcTime: 0,
    });
}
