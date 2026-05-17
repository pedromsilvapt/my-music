import {useCallback} from "react";
import {getMetadataFetchSongSongId} from "../client/metadata-fetch";
import type {SongMetadataDiff} from "../model/songMetadataDiff";
import type {SearchMetadataResult} from "../model";

/**
 * Manual fetch metadata from a specific source song selected by the user.
 *
 * This is the "manual search" flow: after the user searches across all sources
 * and selects a specific result, this hook fetches the full metadata for that
 * source song via GET /metadata-fetch/song/{songId}?sourceId=X&sourceSongId=Y.
 *
 * The server fetches directly from the specified source at request time and
 * returns the metadata diff. Unlike the prefetch endpoint, no audit-rule-based
 * preSelectedFields are returned (the server returns an empty list).
 */
export function useManualFetchMetadata(
    onFetch: (metadata: SongMetadataDiff) => void,
) {
    const manualFetch = useCallback(async (songId: number, result: SearchMetadataResult) => {
        try {
            const response = await getMetadataFetchSongSongId(songId, {
                sourceId: result.sourceId,
                sourceSongId: result.song.id,
            });
            const metadataResponse = response.data;
            if (metadataResponse.hasMetadata && metadataResponse.metadata) {
                onFetch(metadataResponse.metadata);
            }
        } catch {
            // Silently fail - user can try again
        }
    }, [onFetch]);

    return { manualFetch };
}