import {useCallback} from "react";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useTranslation} from "react-i18next";
import {getGetSongHistoryQueryKey} from "../client/song-history.ts";
import {getGetLocalSongQueryKey, useReplaceSongFile} from "../client/songs.ts";
import type {IFormFile} from "../model";

/**
 * Replaces the audio of a song with an uploaded file, reporting the outcome in a notification.
 *
 * The change is recorded as a new version of the song, so the song and its history are refetched. Orval cannot
 * generate the cross-file getGetSongHistoryQueryKey import, so that invalidation happens here.
 */
export function useReplaceSongFileWithNotifications() {
    const {t} = useTranslation(["songs", "common"]);
    const queryClient = useQueryClient();

    const showError = useCallback((error: string) => {
        notifications.show({
            title: t("common:status.error"),
            message: t("songs:tools.uploadSong.failed", {error}),
            color: "red",
        });
    }, [t]);

    const replaceFile = useReplaceSongFile({
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
                    message: t("songs:tools.uploadSong.saved"),
                    color: "green",
                });
            },
            onError: (error) => {
                showError(String(error));
            },
        },
    });

    /** Uploads the file, calling onReplaced once the server has accepted it. */
    const replace = useCallback((songId: number, file: File, onReplaced?: () => void) => {
        // Orval types the multipart field after the server's IFormFile, but sends it as is in a FormData
        replaceFile.mutate({id: songId, data: {file: file as unknown as IFormFile}}, {
            onSuccess: (response) => {
                if (response.status < 400) {
                    onReplaced?.();
                }
            },
        });
    }, [replaceFile]);

    return {
        replace,
        isPending: replaceFile.isPending,
    };
}
