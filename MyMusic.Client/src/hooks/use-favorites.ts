import {useQueryClient} from "@tanstack/react-query";
import {useCallback, useMemo, useRef} from "react";
import {getGetFavoritesQueryKey} from "../client/playlists";
import {useToggleFavorites as useToggleFavoritesMutation, useToggleSongFavorite} from "../client/songs.ts";

// Wrap TanStack Query mutations in useRef to avoid unstable references in dependency arrays.
// Mutations return a new object every render, which would cause unnecessary re-renders
// when passed as props or used in useCallback dependency arrays.
export function useToggleFavorite(onSuccess?: (data: { data: { isFavorite: boolean } }) => void) {
    const queryClient = useQueryClient();
    const mutationRef = useRef(useToggleSongFavorite({}));

    const mutate = useCallback((variables: { id: number }) => {
        mutationRef.current.mutate(variables, {
            onSuccess: (data: { data: { isFavorite: boolean } }) => {
                queryClient.invalidateQueries({queryKey: ['api', 'songs']});
                queryClient.invalidateQueries({queryKey: getGetFavoritesQueryKey()});
                onSuccess?.(data);
            }
        });
    }, [queryClient, onSuccess]);

    return useMemo(() => ({ mutate }), [mutate]);
}

export function useToggleFavorites(onSuccess?: (data: { data: { songs: Array<{ id: number; isFavorite: boolean }> } }) => void) {
    const queryClient = useQueryClient();
    const mutationRef = useRef(useToggleFavoritesMutation({}));

    const mutate = useCallback((variables: { data: { ids: number[] } }) => {
        mutationRef.current.mutate(variables, {
            onSuccess: (data: { data: { songs: Array<{ id: number; isFavorite: boolean }> } }) => {
                queryClient.invalidateQueries({queryKey: ['api', 'songs']});
                queryClient.invalidateQueries({queryKey: getGetFavoritesQueryKey()});
                onSuccess?.(data);
            }
        });
    }, [queryClient, onSuccess]);

    return useMemo(() => ({ mutate }), [mutate]);
}
