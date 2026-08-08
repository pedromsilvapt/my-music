import {useQueryClient} from '@tanstack/react-query';
import {useImportSharedSong} from '../client/song-sharing';
import {getListSongsQueryKey} from '../client/songs';

type ImportSharedSongOptions = NonNullable<Parameters<typeof useImportSharedSong>[0]>;

/**
 * Wrapper for the Orval-generated useImportSharedSong that also invalidates the
 * songs list on success. Orval cannot generate the cross-file getListSongsQueryKey
 * import, so invalidation happens here (skipInvalidation bypasses the generated one).
 */
export function useImportSharedSongWithSongsInvalidation(options?: ImportSharedSongOptions) {
    const queryClient = useQueryClient();

    return useImportSharedSong({
        ...options,
        mutation: {
            ...options?.mutation,
            onSuccess: (data, variables, onMutateResult, context) => {
                queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
                options?.mutation?.onSuccess?.(data, variables, onMutateResult, context);
            },
        },
    });
}
