import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import type {MergeableArtist} from "./artist-merge-modal.tsx";

/**
 * Returns a function that opens the dialog that merges two or more artists into the one the user picks.
 */
export function useMergeArtistsModal() {
    const {t} = useTranslation(["artists"]);

    return useCallback((artists: MergeableArtist[], onSuccess?: () => void) => {
        modals.openContextModal({
            modal: 'artist-merge',
            title: t("artists:merge.title", {count: artists.length}),
            centered: true,
            innerProps: {
                artists: artists.map(({id, name, photo, albumsCount, songsCount}) =>
                    ({id, name, photo, albumsCount, songsCount})),
                onSuccess,
            },
        });
    }, [t]);
}
