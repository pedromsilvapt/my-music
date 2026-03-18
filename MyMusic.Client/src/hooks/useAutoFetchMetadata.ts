import {useQueryClient} from "@tanstack/react-query";
import {useEffect} from "react";
import {useGetMetadataFetchSongSongId, getMetadataFetchSongSongId} from "../client/metadata-fetch";
import type {AutoFetchedMetadataResponse} from "../model";

export type {AutoFetchedMetadataResponse};

export interface SongMetadataDiff {
    title?: MetadataField<string>;
    year?: MetadataField<number>;
    lyrics?: MetadataField<string>;
    rating?: MetadataField<number>;
    explicit?: MetadataField<boolean>;
    cover?: MetadataField<string>;
    album?: MetadataField<MetadataAlbum>;
    albumArtist?: MetadataField<string>;
    artists?: MetadataField<MetadataArtist[]>;
    genres?: MetadataField<string[]>;
}

export interface MetadataField<T> {
    old: T;
    new: T;
}

export interface MetadataAlbum {
    name: string;
    artistName?: string;
}

export interface MetadataArtist {
    name: string;
}

const STALE_TIME_MS = 1000 * 60 * 5; // 5 minutes

/**
 * Hook to fetch auto-fetched metadata for a single song.
 */
export function useAutoFetchMetadata(songId: number | null) {
    return useGetMetadataFetchSongSongId(
        songId ?? 0,
        {
            query: {
                enabled: songId !== null,
                staleTime: STALE_TIME_MS,
            },
        }
    );
}

/**
 * Hook to prefetch metadata for the next song(s) in a sequence.
 * Use this in multi-song editing scenarios.
 */
export function usePrefetchMetadata() {
    const queryClient = useQueryClient();

    return {
        /**
         * Prefetch metadata for a specific song.
         * This will load the data into the cache but won't trigger a re-render.
         */
        prefetch: async (songId: number) => {
            await queryClient.prefetchQuery({
                queryKey: ["api", "metadata-fetch", "song", songId],
                queryFn: () => getMetadataFetchSongSongId(songId),
                staleTime: STALE_TIME_MS,
            });
        },

        /**
         * Get cached metadata for a song if it exists.
         */
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

    // Current song metadata query
    const currentSongId = songIds[currentIndex] ?? null;
    const currentQuery = useAutoFetchMetadata(currentSongId);

    // Prefetch-ahead logic: when current changes, prefetch next
    const prefetchMetadata = usePrefetchMetadata();

    // Use effect to prefetch next song
    useEffect(() => {
        if (currentIndex < songIds.length - 1) {
            const nextSongId = songIds[currentIndex + 1];
            // Fire and forget - no await needed
            prefetchMetadata.prefetch(nextSongId);
        }
    }, [currentIndex, songIds, prefetchMetadata]);

    return {
        currentQuery,
        getMetadata: (songId: number): AutoFetchedMetadataResponse | undefined => {
            const cached = queryClient.getQueryData<{ data: AutoFetchedMetadataResponse }>(["api", "metadata-fetch", "song", songId]);
            return cached?.data;
        },
    };
}