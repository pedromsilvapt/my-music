import {useQueryClient} from '@tanstack/react-query';
import {useDeleteSongShare} from '../client/song-sharing';
import {getListSongsQueryKey} from '../client/songs';

type DeleteSongShareOptions = NonNullable<Parameters<typeof useDeleteSongShare>[0]>;

/**
 * Wrapper for the Orval-generated useDeleteSongShare that also invalidates the
 * songs list on success. Orval cannot generate the cross-file getListSongsQueryKey
 * import, so invalidation happens here (skipInvalidation bypasses the generated one).
 */
export function useDeleteSongShareWithSongsInvalidation(options?: DeleteSongShareOptions) {
    const queryClient = useQueryClient();

    return useDeleteSongShare({
        ...options,
        skipInvalidation: true,
        mutation: {
            ...options?.mutation,
            onSuccess: (data, variables, onMutateResult, context) => {
                queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
                options?.mutation?.onSuccess?.(data, variables, onMutateResult, context);
            },
        },
    });
}
