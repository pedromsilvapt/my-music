import {useQuery} from '@tanstack/react-query';
import {querySongDevices} from '../client/songs';
import {throwOnProblem} from '../utils/api-problem';

/** The key of every {@link useSongDevicesQuery}, to invalidate them when songs are added to or removed from devices. */
export const songDevicesQueryKey = ['api', 'songs', 'devices', 'query'] as const;

/**
 * Where the given songs are on the user's devices (every copy of them) and the paths they would get on the
 * devices they are not on. Wraps the Orval-generated querySongDevices as a query: the endpoint is a POST (a
 * selection of any size fits in its body), which Orval only exposes as a mutation.
 */
export function useSongDevicesQuery(songIds: number[], enabled: boolean) {
    return useQuery({
        queryKey: [...songDevicesQueryKey, songIds],
        queryFn: async ({signal}) => throwOnProblem(await querySongDevices({songIds}, {signal})).data,
        enabled: enabled && songIds.length > 0,
        // Always asked again: the songs' devices may have changed since the dialog was last open
        gcTime: 0,
    });
}
