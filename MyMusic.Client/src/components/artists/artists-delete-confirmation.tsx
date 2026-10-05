import {Alert, Stack, Text} from "@mantine/core";
import {IconAlertTriangle} from "@tabler/icons-react";
import {useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useArtistsUsage} from "../../hooks/use-artists-usage.ts";

interface ArtistsDeleteConfirmationProps {
    artists: { id: number; name: string }[];
}

/**
 * Body of the artist deletion dialog: warns when songs still reference the artists (as one of their
 * artists, or through one of their albums), which the deletion rewrites.
 */
export default function ArtistsDeleteConfirmation({artists}: ArtistsDeleteConfirmationProps) {
    const {t} = useTranslation(["artists", "common"]);
    const artistIds = useMemo(() => artists.map(artist => artist.id), [artists]);
    const usageQuery = useArtistsUsage(artistIds);
    const songsCount = usageQuery.data?.songsCount ?? 0;
    const single = artists.length === 1;

    return (
        <Stack gap="sm" data-testid="artist-delete-confirmation"
               data-loading={usageQuery.isFetching ? "true" : "false"}>
            <Text size="sm">
                {single
                    ? t("artists:delete.confirmSingle", {name: artists[0]!.name})
                    : t("artists:delete.confirmPlural", {count: artists.length})}
            </Text>
            {songsCount > 0 && (
                <Alert color="orange" icon={<IconAlertTriangle/>} data-testid="artist-delete-warning">
                    {single
                        ? t("artists:delete.songsWarningSingle", {count: songsCount})
                        : t("artists:delete.songsWarningPlural", {count: songsCount})}
                </Alert>
            )}
        </Stack>
    );
}
