import {useMutation, useQueryClient} from "@tanstack/react-query";
import {getToggleSongFavoriteMutationOptions, getToggleFavoritesMutationOptions} from "../client/songs.ts";

export function useToggleFavorite(options?: {
    mutation?: {
        onSuccess?: (data: { data: { isFavorite: boolean } }) => void;
    };
}) {
    const queryClient = useQueryClient();

    return useMutation(
        getToggleSongFavoriteMutationOptions(queryClient, {
            mutation: {
                onSuccess: (data: { data: { isFavorite: boolean } }) => {
                    queryClient.invalidateQueries({queryKey: ['api', 'songs']});
                    queryClient.invalidateQueries({queryKey: ['api', 'playlists']});
                    options?.mutation?.onSuccess?.(data);
                }
            }
        }),
        queryClient
    );
}

export function useToggleFavorites(options?: {
    mutation?: {
        onSuccess?: (data: { data: { songs: Array<{ id: number; isFavorite: boolean }> } }) => void;
    };
}) {
    const queryClient = useQueryClient();

    return useMutation(
        getToggleFavoritesMutationOptions({
            mutation: {
                onSuccess: (data: { data: { songs: Array<{ id: number; isFavorite: boolean }> } }) => {
                    queryClient.invalidateQueries({queryKey: ['api', 'songs']});
                    queryClient.invalidateQueries({queryKey: ['api', 'playlists']});
                    options?.mutation?.onSuccess?.(data);
                }
            }
        }),
        queryClient
    );
}
