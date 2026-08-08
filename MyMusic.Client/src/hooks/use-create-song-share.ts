import {useQueryClient} from '@tanstack/react-query';
import {useCreateSongShare} from '../client/song-sharing';
import {getListSongsQueryKey} from '../client/songs';

type CreateSongShareOptions = NonNullable<Parameters<typeof useCreateSongShare>[0]>;

/**
 * Wrapper for the Orval-generated useCreateSongShare that also invalidates the
 * songs list on success. Orval cannot generate the cross-file getListSongsQueryKey
 * import, so invalidation happens here (skipInvalidation bypasses the generated one).
 */
export function useCreateSongShareWithSongsInvalidation(options?: CreateSongShareOptions) {
    const queryClient = useQueryClient();

    return useCreateSongShare({
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
