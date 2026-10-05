import {type Query, useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {getListAlbumsQueryKey, useDeleteAlbums} from '../client/albums';
import {getListArtistsQueryKey} from '../client/artists';
import {getListSongsQueryKey} from '../client/songs';
import {throwOnProblem} from '../utils/api-problem';

/**
 * Wrapper for the Orval-generated useDeleteAlbums. Returns a function that deletes albums as a single
 * operation, rejecting with an ApiProblemError when the server refuses, and then invalidates everything
 * the deletion changed: the albums, the songs it moved to "(No Album)" and the artists (album counts).
 * Orval cannot generate the cross-file query key imports, so the invalidation happens here.
 */
export function useDeleteAlbumsWithInvalidation() {
    const queryClient = useQueryClient();
    const {mutateAsync} = useDeleteAlbums();

    return useCallback(async (albumIds: number[]) => {
        throwOnProblem(await mutateAsync({data: {albumIds}}));

        // The deleted albums' own queries (details) are left alone, as refetching them would only fail, and
        // so is the usage the confirmation dialog just showed
        const deletedIds = new Set<unknown>(albumIds);
        const isObsolete = (query: Query) => deletedIds.has(query.queryKey[2]) || query.queryKey[2] === 'usage';

        queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey(), predicate: query => !isObsolete(query)});
        queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
    }, [mutateAsync, queryClient]);
}
