import {useQueryClient} from '@tanstack/react-query';
import {useManageSongShares} from '../client/song-sharing';
import {getListSongsQueryKey} from '../client/songs';

type ManageSongSharesOptions = NonNullable<Parameters<typeof useManageSongShares>[0]>;

/**
 * Wrapper for the Orval-generated useManageSongShares that also invalidates the
 * songs list on success. Orval cannot generate the cross-file getListSongsQueryKey
 * import, so invalidation happens here (skipInvalidation bypasses the generated one).
 */
export function useManageSongSharesWithSongsInvalidation(options?: ManageSongSharesOptions) {
    const queryClient = useQueryClient();

    return useManageSongShares({
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
