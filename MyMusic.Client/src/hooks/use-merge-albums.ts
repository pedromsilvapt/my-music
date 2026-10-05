import {useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {getListAlbumsQueryKey, useMergeAlbums} from '../client/albums';
import {getListArtistsQueryKey} from '../client/artists';
import {getListSongsQueryKey} from '../client/songs';
import {throwOnProblem} from '../utils/api-problem';

/**
 * Wrapper for the Orval-generated useMergeAlbums. Returns a function that merges albums into another one as a
 * single operation, rejecting with an ApiProblemError when the server refuses, and then invalidates everything
 * the merge changed: the albums (lists and details), the songs that moved and the artists listing them.
 * Orval cannot generate the cross-file query key imports, so the invalidation happens here.
 */
export function useMergeAlbumsWithInvalidation() {
    const queryClient = useQueryClient();
    const {mutateAsync} = useMergeAlbums();

    return useCallback(async (targetId: number, sourceIds: number[]) => {
        throwOnProblem(await mutateAsync({data: {targetId, sourceIds}}));

        queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
    }, [mutateAsync, queryClient]);
}
