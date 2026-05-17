import {useQueryClient} from "@tanstack/react-query";
import {useEffect} from "react";
import {useGetMetadataFetchSongSongId, getMetadataFetchSongSongId} from "../client/metadata-fetch";
import type {AutoFetchedMetadataResponse} from "../model";

export type {AutoFetchedMetadataResponse};

const STALE_TIME_MS = 1000 * 60 * 5;

/**
 * Prefetch metadata from the AutoFetchedMetadata database table for a single song.
 *
 * This is the "prefetch" mechanism: when the Song Edit Modal opens from an audit rule page,
 * it reads previously auto-fetched metadata from the database (not from a live source).
 * The server returns `preSelectedFields` based on the audit rules that flagged the song.
 *
 * Do NOT use this when the modal opens from a non-audit context (songs page, song detail page).
 * In those contexts there is no pre-fetched metadata to load, and the query should stay disabled.
 *
 * For the "auto" button (live source fetch), use useAutoFetchMetadata from useAutoFetchMetadata.ts.
 * For the "manual search" flow (user picks a source result), use getMetadataFetchSongSongId with sourceId/sourceSongId.
 */
export function usePrefetchMetadata(songId: number | null, enabled: boolean = true) {
    return useGetMetadataFetchSongSongId(
        songId ?? 0,
        undefined,
        {
            query: {
                enabled: songId !== null && enabled,
                staleTime: STALE_TIME_MS,
            },
        }
    );
}

/**
 * Hook for prefetch-ahead support in multi-song editing scenarios.
 *
 * When editing multiple songs from an audit page, this preloads metadata for the next song(s)
 * so they're ready in the cache when the user navigates forward.
 */
export function useMetadataPrefetchAhead() {
    const queryClient = useQueryClient();

    return {
        prefetch: async (songId: number) => {
            await queryClient.prefetchQuery({
                queryKey: ["api", "metadata-fetch", "song", songId],
                queryFn: () => getMetadataFetchSongSongId(songId, undefined),
                staleTime: STALE_TIME_MS,
            });
        },

        getCached: (songId: number): AutoFetchedMetadataResponse | undefined => {
            const cached = queryClient.getQueryData<{ data: AutoFetchedMetadataResponse }>(["api", "metadata-fetch", "song", songId]);
            return cached?.data;
        },
    };
}

/**
 * Hook to manage metadata fetching for multiple songs with prefetch-ahead support.
 *
 * Usage:
 * - When on song N, call `prefetchNext(songIds[N+1])` to preload next song
 * - Use `getMetadata(songId)` to get cached metadata for any song
 */
export function useMultiSongMetadata(songIds: number[], currentIndex: number) {
    const queryClient = useQueryClient();

    const currentSongId = songIds[currentIndex] ?? null;
    const currentQuery = usePrefetchMetadata(currentSongId);

    const prefetchAhead = useMetadataPrefetchAhead();

    useEffect(() => {
        if (currentIndex < songIds.length - 1) {
            const nextSongId = songIds[currentIndex + 1];
            prefetchAhead.prefetch(nextSongId);
        }
    }, [currentIndex, songIds, prefetchAhead]);

    return {
        currentQuery,
        getMetadata: (songId: number): AutoFetchedMetadataResponse | undefined => {
            const cached = queryClient.getQueryData<{ data: AutoFetchedMetadataResponse }>(["api", "metadata-fetch", "song", songId]);
            return cached?.data;
        },
    };
}