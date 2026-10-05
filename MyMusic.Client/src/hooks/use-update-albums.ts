import {useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {getListAlbumsQueryKey, useUpdateAlbums} from '../client/albums';
import {getListArtistsQueryKey} from '../client/artists';
import {getListSongsQueryKey} from '../client/songs';
import type {UpdateAlbumsItem} from '../model';
import {throwOnProblem} from '../utils/api-problem';

/**
 * Wrapper for the Orval-generated useUpdateAlbums. Returns a function that edits albums as a single
 * operation, rejecting with an ApiProblemError when the server refuses, and then invalidates everything
 * the edit changed: the albums (lists and details), the songs that name them and the artists listing them.
 * Orval cannot generate the cross-file query key imports, so the invalidation happens here.
 */
export function useUpdateAlbumsWithInvalidation() {
    const queryClient = useQueryClient();
    const {mutateAsync} = useUpdateAlbums();

    return useCallback(async (albums: UpdateAlbumsItem[]) => {
        throwOnProblem(await mutateAsync({data: {albums}}));

        queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
    }, [mutateAsync, queryClient]);
}
