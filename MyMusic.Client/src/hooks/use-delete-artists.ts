import {type Query, useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {getListArtistsQueryKey, useDeleteArtists} from '../client/artists';
import {getListAlbumsQueryKey} from '../client/albums';
import {getListSongsQueryKey} from '../client/songs';
import {throwOnProblem} from '../utils/api-problem';

/**
 * Wrapper for the Orval-generated useDeleteArtists. Returns a function that deletes artists as a single
 * operation, rejecting with an ApiProblemError when the server refuses, and then invalidates everything
 * the deletion changed: the artists, their albums (deleted too) and the songs they were taken out of.
 * Orval cannot generate the cross-file query key imports, so the invalidation happens here.
 */
export function useDeleteArtistsWithInvalidation() {
    const queryClient = useQueryClient();
    const {mutateAsync} = useDeleteArtists();

    return useCallback(async (artistIds: number[]) => {
        throwOnProblem(await mutateAsync({data: {artistIds}}));

        // The deleted artists' own queries (details) are left alone, as refetching them would only fail, and
        // so is the usage the confirmation dialog just showed
        const deletedIds = new Set<unknown>(artistIds);
        const isObsolete = (query: Query) => deletedIds.has(query.queryKey[2]) || query.queryKey[2] === 'usage';

        queryClient.invalidateQueries({queryKey: getListArtistsQueryKey(), predicate: query => !isObsolete(query)});
        queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
    }, [mutateAsync, queryClient]);
}
