import {useQueryClient} from '@tanstack/react-query';
import {useCreateAlbum} from '../client/albums';
import {getListArtistsQueryKey} from '../client/artists';

type CreateAlbumOptions = NonNullable<Parameters<typeof useCreateAlbum>[0]>;

/**
 * Wrapper for the Orval-generated useCreateAlbum that also invalidates the artists, whose album counts
 * change. Orval cannot generate the cross-file getListArtistsQueryKey import, so that invalidation happens
 * here (the albums list is invalidated by the generated hook).
 */
export function useCreateAlbumWithArtistsInvalidation(options?: CreateAlbumOptions) {
    const queryClient = useQueryClient();

    return useCreateAlbum({
        ...options,
        mutation: {
            ...options?.mutation,
            onSuccess: (data, variables, onMutateResult, context) => {
                queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
                options?.mutation?.onSuccess?.(data, variables, onMutateResult, context);
            },
        },
    });
}
