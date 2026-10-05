import {useQueryClient} from "@tanstack/react-query";
import {useResolveSoundalikes as useResolveSoundalikesMutation} from "../client/audits.ts";
import {getListAlbumsQueryKey} from "../client/albums.ts";
import {getListArtistsQueryKey} from "../client/artists.ts";
import {getListPlaylistsQueryKey} from "../client/playlists.ts";
import {getListSongsQueryKey} from "../client/songs.ts";

/**
 * Resolves groups of soundalikes. On top of the audit queries the generated mutation refreshes, the songs merged
 * away are gone from every songs, albums, artists and playlists query.
 */
export function useResolveSoundalikes() {
    const queryClient = useQueryClient();

    return useResolveSoundalikesMutation({
        mutation: {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    throw new Error(`Failed to resolve duplicates (${response.status})`);
                }

                queryClient.invalidateQueries({queryKey: getListSongsQueryKey()});
                queryClient.invalidateQueries({queryKey: getListAlbumsQueryKey()});
                queryClient.invalidateQueries({queryKey: getListArtistsQueryKey()});
                queryClient.invalidateQueries({queryKey: getListPlaylistsQueryKey()});
            },
        },
    });
}
