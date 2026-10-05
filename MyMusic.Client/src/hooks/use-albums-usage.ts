import {useQuery} from '@tanstack/react-query';
import {getAlbumsUsage, getListAlbumsQueryKey} from '../client/albums';

/**
 * How many songs deleting the given albums would change. Wraps the Orval-generated getAlbumsUsage as a
 * query: the endpoint is a POST (a selection of any size fits in its body), which Orval only exposes
 * as a mutation.
 */
export function useAlbumsUsage(albumIds: number[]) {
    return useQuery({
        queryKey: [...getListAlbumsQueryKey(), 'usage', albumIds],
        queryFn: ({signal}) => getAlbumsUsage({albumIds}, {signal}),
        select: response => response.status < 400 ? response.data : undefined,
        // Always asked again: the songs may have changed since the dialog was last open
        gcTime: 0,
    });
}
