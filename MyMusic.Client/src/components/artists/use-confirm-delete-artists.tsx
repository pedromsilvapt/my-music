import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import {useConfirmDelete} from "../../hooks/use-confirm-delete.ts";
import {useDeleteArtistsWithInvalidation} from "../../hooks/use-delete-artists.ts";
import {ApiProblemError} from "../../utils/api-problem.ts";
import ArtistsDeleteConfirmation from "./artists-delete-confirmation.tsx";

/**
 * Returns a function that asks to confirm the deletion of one or more artists, warning about the songs
 * that still reference them, and deletes them all as a single operation.
 */
export function useConfirmDeleteArtists() {
    const {t} = useTranslation(["artists", "common"]);
    const confirmDelete = useConfirmDelete();
    const deleteArtists = useDeleteArtistsWithInvalidation();

    return useCallback((artists: { id: number; name: string }[], onSuccess?: () => void) => {
        const single = artists.length === 1;

        confirmDelete({
            title: single ? t("artists:delete.titleSingle") : t("artists:delete.titlePlural", {count: artists.length}),
            children: <ArtistsDeleteConfirmation artists={artists}/>,
            onConfirm: () => deleteArtists(artists.map(artist => artist.id)),
            onSuccess,
            // The server explains why it refused (e.g. a placeholder artist that still has songs)
            errorMessage: error => (error instanceof ApiProblemError ? error.detail : undefined)
                ?? (single
                    ? t("artists:delete.failedSingle", {name: artists[0]!.name})
                    : t("artists:delete.failedPlural", {count: artists.length})),
        });
    }, [confirmDelete, deleteArtists, t]);
}
