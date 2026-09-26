import {useQueryClient} from '@tanstack/react-query';
import {useManagePlaylistShares} from '../client/playlist-sharing';
import {getListPlaylistsQueryKey} from '../client/playlists';
import {getListSongsQueryKey} from '../client/songs';

type ManagePlaylistSharesOptions = NonNullable<Parameters<typeof useManagePlaylistShares>[0]>;

/**
 * Wrapper for the Orval-generated useManagePlaylistShares that also invalidates the songs list
 * and the playlists (list + detail, both under the ['api', 'playlists'] prefix). Orval cannot
 * generate cross-file query key imports, and the playlists page uses its own ["playlists"] key.
 */
export function useManagePlaylistSharesWithInvalidation(options?: ManagePlaylistSharesOptions) {
    const queryClient = useQueryClient();

    return useManagePlaylistShares({
        ...options,
        mutation: {
            ...options?.mutation,
            onSuccess: (data, variables, onMutateResult, context) => {
                queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
                queryClient.invalidateQueries({queryKey: getListPlaylistsQueryKey()});
                queryClient.invalidateQueries({queryKey: ["playlists"]});
                options?.mutation?.onSuccess?.(data, variables, onMutateResult, context);
            },
        },
    });
}
