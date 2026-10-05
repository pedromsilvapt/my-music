import {Alert, Stack, Text} from "@mantine/core";
import {IconAlertTriangle} from "@tabler/icons-react";
import {useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useAlbumsUsage} from "../../hooks/use-albums-usage.ts";

interface AlbumsDeleteConfirmationProps {
    albums: { id: number; name: string }[];
}

/**
 * Body of the album deletion dialog: warns when the albums still have songs, which the deletion moves
 * to the placeholder album.
 */
export default function AlbumsDeleteConfirmation({albums}: AlbumsDeleteConfirmationProps) {
    const {t} = useTranslation(["albums", "common"]);
    const albumIds = useMemo(() => albums.map(album => album.id), [albums]);
    const usageQuery = useAlbumsUsage(albumIds);
    const songsCount = usageQuery.data?.songsCount ?? 0;
    const single = albums.length === 1;

    return (
        <Stack gap="sm" data-testid="album-delete-confirmation"
               data-loading={usageQuery.isFetching ? "true" : "false"}>
            <Text size="sm">
                {single
                    ? t("albums:delete.confirmSingle", {name: albums[0]!.name})
                    : t("albums:delete.confirmPlural", {count: albums.length})}
            </Text>
            {songsCount > 0 && (
                <Alert color="orange" icon={<IconAlertTriangle/>} data-testid="album-delete-warning">
                    {single
                        ? t("albums:delete.songsWarningSingle", {count: songsCount})
                        : t("albums:delete.songsWarningPlural", {count: songsCount})}
                </Alert>
            )}
        </Stack>
    );
}
