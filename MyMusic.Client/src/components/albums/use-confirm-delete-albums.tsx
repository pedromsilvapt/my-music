import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import {useConfirmDelete} from "../../hooks/use-confirm-delete.ts";
import {useDeleteAlbumsWithInvalidation} from "../../hooks/use-delete-albums.ts";
import {ApiProblemError} from "../../utils/api-problem.ts";
import AlbumsDeleteConfirmation from "./albums-delete-confirmation.tsx";

/**
 * Returns a function that asks to confirm the deletion of one or more albums, warning about the songs
 * that still reference them, and deletes them all as a single operation.
 */
export function useConfirmDeleteAlbums() {
    const {t} = useTranslation(["albums", "common"]);
    const confirmDelete = useConfirmDelete();
    const deleteAlbums = useDeleteAlbumsWithInvalidation();

    return useCallback((albums: { id: number; name: string }[], onSuccess?: () => void) => {
        const single = albums.length === 1;

        confirmDelete({
            title: single ? t("albums:delete.titleSingle") : t("albums:delete.titlePlural", {count: albums.length}),
            children: <AlbumsDeleteConfirmation albums={albums}/>,
            onConfirm: () => deleteAlbums(albums.map(album => album.id)),
            onSuccess,
            // The server explains why it refused (e.g. a placeholder album that still has songs)
            errorMessage: error => (error instanceof ApiProblemError ? error.detail : undefined)
                ?? (single
                    ? t("albums:delete.failedSingle", {name: albums[0]!.name})
                    : t("albums:delete.failedPlural", {count: albums.length})),
        });
    }, [confirmDelete, deleteAlbums, t]);
}
