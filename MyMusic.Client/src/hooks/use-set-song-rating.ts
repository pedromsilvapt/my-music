import {useCallback, useMemo, useRef} from "react";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useTranslation} from "react-i18next";
import {useUpdateSong} from "../client/songs.ts";

/**
 * Sets (or clears, with null) the rating of a single song, without opening the edit modal.
 *
 * The rating goes through the regular song update, so it is also written to the file tags. Besides the song
 * queries, the queue and playlists are refetched so the footer player and playlist pages show the new rating;
 * Orval cannot generate these cross-file invalidations, so they happen here.
 *
 * The returned object is stable across renders, so it can be used inside memoized collection schemas.
 */
export function useSetSongRating() {
    const {t} = useTranslation(["songs", "common"]);
    const queryClient = useQueryClient();
    const updateSong = useUpdateSong();

    // Keep the latest mutation and translation in refs so `mutate` never changes identity
    const updateSongRef = useRef(updateSong);
    updateSongRef.current = updateSong;
    const tRef = useRef(t);
    tRef.current = t;

    const showError = useCallback((error: string) => {
        notifications.show({
            title: tRef.current("common:status.error"),
            message: tRef.current("songs:schema.ratingFailed", {error}),
            color: "red",
        });
    }, []);

    const mutate = useCallback((songId: number, rating: number | null) => {
        updateSongRef.current.mutate({id: songId, data: {songId, rating: {newValue: rating}}}, {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    showError(responseData?.detail || tRef.current("songs:editModal.unknownError"));
                    return;
                }

                void queryClient.invalidateQueries({queryKey: ["api", "songs"]});
                void queryClient.invalidateQueries({queryKey: ["api", "playlists"]});
                void queryClient.invalidateQueries({queryKey: ["api", "audits"]});
            },
            onError: (error) => showError(String(error)),
        });
    }, [queryClient, showError]);

    return useMemo(() => ({mutate}), [mutate]);
}
