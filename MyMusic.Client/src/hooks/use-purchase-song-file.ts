import {useCallback} from "react";
import {notifications} from "@mantine/notifications";
import {useTranslation} from "react-i18next";
import {useCreatePurchase} from "../client/purchases.ts";

/**
 * Purchases a song of a source to replace the audio of a song of the library with it, reporting the outcome in a
 * notification.
 *
 * The purchase only joins the purchases queue here: the audio is replaced once it gets acquired.
 */
export function usePurchaseSongFileWithNotifications() {
    const {t} = useTranslation(["songs", "common"]);
    const createPurchase = useCreatePurchase();
    const {mutateAsync} = createPurchase;

    /** Queues the purchase, resolving to whether the server has accepted it. */
    const purchase = useCallback(async (songId: number, sourceId: number, sourceSongId: string) => {
        const showError = (error: string) => notifications.show({
            title: t("common:status.error"),
            message: t("songs:tools.purchaseSong.failed", {error}),
            color: "red",
        });

        try {
            const response = await mutateAsync({sourceId, songId: sourceSongId, params: {replaceSongId: songId}});

            if (response.status >= 400) {
                const responseData = response.data as { detail?: string } | undefined;
                showError(responseData?.detail || t("songs:editModal.unknownError"));
                return false;
            }
        } catch (error) {
            showError(String(error));
            return false;
        }

        notifications.show({
            title: t("common:status.success"),
            message: t("songs:tools.purchaseSong.queued"),
            color: "green",
        });

        return true;
    }, [mutateAsync, t]);

    return {
        purchase,
        isPending: createPurchase.isPending,
    };
}
