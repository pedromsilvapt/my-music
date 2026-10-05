import {useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {getListAlbumsQueryKey} from '../client/albums';
import {getListArtistsQueryKey, useMergeArtists} from '../client/artists';
import {getListSongsQueryKey} from '../client/songs';
import {throwOnProblem} from '../utils/api-problem';

/**
 * Wrapper for the Orval-generated useMergeArtists. Returns a function that merges artists into another one as a
 * single operation, rejecting with an ApiProblemError when the server refuses, and then invalidates everything
 * the merge changed: the artists (lists and details), the songs that name them and the albums they owned.
 * Orval cannot generate the cross-file query key imports, so the invalidation happens here.
 */
export function useMergeArtistsWithInvalidation() {
    const queryClient = useQueryClient();
    const {mutateAsync} = useMergeArtists();

    return useCallback(async (targetId: number, sourceIds: number[]) => {
        throwOnProblem(await mutateAsync({data: {targetId, sourceIds}}));

        queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
    }, [mutateAsync, queryClient]);
}
