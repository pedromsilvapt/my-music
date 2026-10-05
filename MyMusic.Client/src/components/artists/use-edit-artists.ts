import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import type {EditableArtist} from "./artist-editor-modal.tsx";

/**
 * Returns a function that opens the editor for one or more artists, which are saved together as a single
 * operation.
 */
export function useEditArtists() {
    const {t} = useTranslation(["artists"]);

    return useCallback((artists: EditableArtist[], onSuccess?: () => void) => {
        modals.openContextModal({
            modal: 'artist-editor',
            title: artists.length === 1
                ? t("artists:editModal.titleSingle")
                : t("artists:editModal.titlePlural", {count: artists.length}),
            centered: true,
            innerProps: {
                artists: artists.map(({id, name}) => ({id, name})),
                onSuccess,
            },
        });
    }, [t]);
}
