import {useCallback} from "react";
import {notifications} from "@mantine/notifications";
import {useFetchSongMetadata} from "../client/songs.ts";
import type {SongMetadataDiff} from "../model/songMetadataDiff";

/**
 * Auto-fetch metadata from all configured sources (live search).
 *
 * This is the "auto" button behavior: it searches all configured sources for the song,
 * picks the best match, and fetches full metadata details directly from that source.
 * It always goes to the live source -- it does NOT read from the AutoFetchedMetadata database table.
 *
 * When used in an audit context (the modal was opened from an audit rule page),
 * the `onFetch` callback should merge the new metadata with the existing pre-selected fields
 * from audit rules, rather than replacing the checkbox state entirely.
 */
export function useAutoFetchMetadata(
    onFetch: (metadata: SongMetadataDiff) => void,
) {
    const fetchMetadata = useFetchSongMetadata({
        mutation: {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    const errorDetail = responseData?.detail || "Unknown error";
                    notifications.show({title: "Error", message: `Failed to fetch metadata: ${errorDetail}`, color: "red"});
                    return;
                }
                if (response.data.metadata) {
                    onFetch(response.data.metadata);
                }
            },
            onError: (error) => {
                notifications.show({title: "Error", message: `Failed to fetch metadata: ${error}`, color: "red"});
            },
        },
    });

    const autoFetch = useCallback((songId: number) => {
        fetchMetadata.mutate({ id: songId });
    }, [fetchMetadata]);

    return {
        autoFetch,
        isPending: fetchMetadata.isPending,
    };
}