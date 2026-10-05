import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import type {MergeableAlbum} from "./album-merge-modal.tsx";

/**
 * Returns a function that opens the dialog that merges two or more albums into the one the user picks.
 */
export function useMergeAlbumsModal() {
    const {t} = useTranslation(["albums"]);

    return useCallback((albums: MergeableAlbum[], onSuccess?: () => void) => {
        modals.openContextModal({
            modal: 'album-merge',
            title: t("albums:merge.title", {count: albums.length}),
            centered: true,
            innerProps: {
                albums: albums.map(({id, name, cover, year, songsCount}) => ({id, name, cover, year, songsCount})),
                onSuccess,
            },
        });
    }, [t]);
}
