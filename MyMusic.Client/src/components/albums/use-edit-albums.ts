import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import type {EditableAlbum} from "./album-editor-modal.tsx";

/**
 * Returns a function that opens the editor for one or more albums, which are saved together as a single
 * operation.
 */
export function useEditAlbums() {
    const {t} = useTranslation(["albums"]);

    return useCallback((albums: EditableAlbum[], onSuccess?: () => void) => {
        modals.openContextModal({
            modal: 'album-editor',
            title: albums.length === 1
                ? t("albums:editModal.titleSingle")
                : t("albums:editModal.titlePlural", {count: albums.length}),
            centered: true,
            innerProps: {
                albums: albums.map(({id, name, year}) => ({id, name, year})),
                onSuccess,
            },
        });
    }, [t]);
}
