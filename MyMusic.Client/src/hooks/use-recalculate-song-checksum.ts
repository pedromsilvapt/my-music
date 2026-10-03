import {useCallback} from "react";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useTranslation} from "react-i18next";
import {getGetSongHistoryQueryKey} from "../client/song-history.ts";
import {getGetLocalSongQueryKey, useRecalculateSongChecksum} from "../client/songs.ts";

/**
 * Recalculates the checksum of a song's file, reporting the outcome (failed, unchanged or changed) in a
 * notification. The notifications are fired from the mutation's own callbacks, so they still show up when the
 * component that started the recalculation is gone by the time it finishes.
 *
 * A changed checksum is recorded as a new version of the song, so the song (for its pending history flag) and its
 * history are refetched. Orval cannot generate the cross-file getGetSongHistoryQueryKey import, so that
 * invalidation happens here.
 */
export function useRecalculateSongChecksumWithNotifications() {
    const {t} = useTranslation(["songs", "common"]);
    const queryClient = useQueryClient();

    const showError = useCallback((error: string) => {
        notifications.show({
            title: t("common:status.error"),
            message: t("songs:tools.recalculateChecksum.failed", {error}),
            color: "red",
        });
    }, [t]);

    const recalculateChecksum = useRecalculateSongChecksum({
        mutation: {
            onSuccess: (response, variables) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    showError(responseData?.detail || t("songs:editModal.unknownError"));
                    return;
                }

                if (!response.data.changed) {
                    notifications.show({
                        title: t("common:status.info"),
                        message: t("songs:tools.recalculateChecksum.unchanged"),
                        color: "blue",
                    });
                    return;
                }

                void queryClient.invalidateQueries({queryKey: getGetLocalSongQueryKey(variables.id)});
                void queryClient.invalidateQueries({queryKey: getGetSongHistoryQueryKey(variables.id)});
                notifications.show({
                    title: t("common:status.success"),
                    message: t("songs:tools.recalculateChecksum.changed"),
                    color: "green",
                });
            },
            onError: (error) => {
                showError(String(error));
            },
        },
    });

    const recalculate = useCallback((songId: number) => {
        recalculateChecksum.mutate({id: songId});
    }, [recalculateChecksum]);

    return {
        recalculate,
        isPending: recalculateChecksum.isPending,
    };
}
