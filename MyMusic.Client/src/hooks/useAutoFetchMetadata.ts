import {useQuery, useQueryClient} from "@tanstack/react-query";

export interface AutoFetchedMetadataResponse {
    hasMetadata: boolean;
    metadata?: SongMetadataDiff;
    fetchedAt?: string;
    sourceName?: string;
    preSelectedFields: string[];
}

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

async function fetchAutoFetchedMetadata(songId: number): Promise<AutoFetchedMetadataResponse> {
    const response = await fetch(`/api/metadata-fetch/song/${songId}`);
    
    if (!response.ok) {
        throw new Error('Failed to fetch metadata');
    }
    
    return response.json();
}

const STALE_TIME_MS = 1000 * 60 * 5; // 5 minutes

/**
 * Hook to fetch auto-fetched metadata for a single song.
 */
export function useAutoFetchMetadata(songId: number | null) {
    return useQuery({
        queryKey: ['metadata-fetch', 'song', songId],
        queryFn: () => fetchAutoFetchedMetadata(songId!),
        enabled: songId !== null,
        staleTime: STALE_TIME_MS,
    });
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
                queryKey: ['metadata-fetch', 'song', songId],
                queryFn: () => fetchAutoFetchedMetadata(songId),
                staleTime: STALE_TIME_MS,
            });
        },
        
        /**
         * Get cached metadata for a song if it exists.
         */
        getCached: (songId: number): AutoFetchedMetadataResponse | undefined => {
            return queryClient.getQueryData(['metadata-fetch', 'song', songId]);
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
    useEffect(() => {
        if (currentIndex < songIds.length - 1) {
            const nextSongId = songIds[currentIndex + 1];
            queryClient.prefetchQuery({
                queryKey: ['metadata-fetch', 'song', nextSongId],
                queryFn: () => fetchAutoFetchedMetadata(nextSongId),
                staleTime: STALE_TIME_MS,
            });
        }
    }, [currentIndex, songIds, queryClient]);
    
    return {
        currentQuery,
        getMetadata: (songId: number): AutoFetchedMetadataResponse | undefined => {
            return queryClient.getQueryData(['metadata-fetch', 'song', songId]);
        },
    };
}

import {useEffect} from "react";
