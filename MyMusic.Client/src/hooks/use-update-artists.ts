import {useQueryClient} from '@tanstack/react-query';
import {useCallback} from 'react';
import {getListAlbumsQueryKey} from '../client/albums';
import {getListArtistsQueryKey, useUpdateArtists} from '../client/artists';
import {getListSongsQueryKey} from '../client/songs';
import type {UpdateArtistsItem} from '../model';
import {throwOnProblem} from '../utils/api-problem';

/**
 * Wrapper for the Orval-generated useUpdateArtists. Returns a function that edits artists as a single
 * operation, rejecting with an ApiProblemError when the server refuses, and then invalidates everything
 * the edit changed: the artists (lists and details), the songs that name them and their albums.
 * Orval cannot generate the cross-file query key imports, so the invalidation happens here.
 */
export function useUpdateArtistsWithInvalidation() {
    const queryClient = useQueryClient();
    const {mutateAsync} = useUpdateArtists();

    return useCallback(async (artists: UpdateArtistsItem[]) => {
        throwOnProblem(await mutateAsync({data: {artists}}));

        queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey()});
        queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
    }, [mutateAsync, queryClient]);
}
