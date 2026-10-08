import {useCallback} from "react";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useTranslation} from "react-i18next";
import {getGetSongHistoryQueryKey} from "../client/song-history.ts";
import {getGetLocalSongQueryKey, useUpdateSongTimestamps} from "../client/songs.ts";
import type {UpdateSongTimestampsRequest} from "../model";

/**
 * Sets the timestamps of a song, reporting the outcome in a notification.
 *
 * The change is recorded as a new version of the song, so the song and its history are refetched. Orval cannot
 * generate the cross-file getGetSongHistoryQueryKey import, so that invalidation happens here.
 */
export function useUpdateSongTimestampsWithNotifications() {
    const {t} = useTranslation(["songs", "common"]);
    const queryClient = useQueryClient();

    const showError = useCallback((error: string) => {
        notifications.show({
            title: t("common:status.error"),
            message: t("songs:tools.changeTimestamps.failed", {error}),
            color: "red",
        });
    }, [t]);

    const updateTimestamps = useUpdateSongTimestamps({
        mutation: {
            onSuccess: (response, variables) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    showError(responseData?.detail || t("songs:editModal.unknownError"));
                    return;
                }

                void queryClient.invalidateQueries({queryKey: getGetLocalSongQueryKey(variables.id)});
                void queryClient.invalidateQueries({queryKey: getGetSongHistoryQueryKey(variables.id)});
                notifications.show({
                    title: t("common:status.success"),
                    message: t("songs:tools.changeTimestamps.saved"),
                    color: "green",
                });
            },
            onError: (error) => {
                showError(String(error));
            },
        },
    });

    /** Saves the timestamps, calling onSaved once the server has accepted them. */
    const update = useCallback((songId: number, timestamps: UpdateSongTimestampsRequest, onSaved?: () => void) => {
        updateTimestamps.mutate({id: songId, data: timestamps}, {
            onSuccess: (response) => {
                if (response.status < 400) {
                    onSaved?.();
                }
            },
        });
    }, [updateTimestamps]);

    return {
        update,
        isPending: updateTimestamps.isPending,
    };
}
