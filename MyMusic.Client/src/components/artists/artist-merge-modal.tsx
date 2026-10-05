import {Alert, Button, Group, Stack, Text} from "@mantine/core";
import {type ContextModalProps, modals} from "@mantine/modals";
import {IconAlertTriangle, IconArrowMerge, IconUser} from "@tabler/icons-react";
import {useCallback, useEffect, useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import {ARTIST_PLACEHOLDER_NAME} from "../../consts.ts";
import {useArtistsMergePreview} from "../../hooks/use-artists-merge-preview.ts";
import {useMergeArtistsWithInvalidation} from "../../hooks/use-merge-artists.ts";
import {ApiProblemError} from "../../utils/api-problem.ts";
import Artwork from "../common/artwork.tsx";
import VirtualSelect from "../common/virtual-select.tsx";

const ARTIST_OPTION_HEIGHT = 56;
const ARTIST_ARTWORK_SIZE = 40;

export interface MergeableArtist {
    id: number;
    name: string;
    photo?: number | null;
    albumsCount?: number | null;
    songsCount?: number | null;
}

export interface ArtistMergeModalInnerProps {
    artists: MergeableArtist[];
    onSuccess?: () => void;
}

const getArtistKey = (artist: MergeableArtist) => artist.id;
const getArtistLabel = (artist: MergeableArtist) => artist.name;

/**
 * The artist kept by default: the placeholder artist, which cannot be merged into another one, or else the
 * artist with the most songs, so the fewest files are rewritten.
 */
const getDefaultTarget = (artists: MergeableArtist[]) =>
    artists.find(artist => artist.name === ARTIST_PLACEHOLDER_NAME)
    ?? artists.reduce((best, artist) => (artist.songsCount ?? 0) > (best.songsCount ?? 0) ? artist : best);

/**
 * Merges the artists picked by the user into one of them: the user chooses the artist to keep, and the others
 * are merged into it as a single operation. The dialog says what the merge changes before it is confirmed,
 * stays open until the server is done rewriting the songs, and shows why when it refuses.
 */
export default function ArtistMergeModal({context, id, innerProps}: ContextModalProps<ArtistMergeModalInnerProps>) {
    const {t} = useTranslation(["artists", "common"]);
    const {artists, onSuccess} = innerProps;
    const [target, setTarget] = useState(() => getDefaultTarget(artists));
    const [error, setError] = useState<string | null>(null);
    const [merging, setMerging] = useState(false);
    const mergeArtists = useMergeArtistsWithInvalidation();

    const sourceIds = useMemo(
        () => artists.filter(artist => artist.id !== target.id).map(artist => artist.id),
        [artists, target]);
    const previewQuery = useArtistsMergePreview(target.id, sourceIds);
    const preview = previewQuery.data;

    // The dialog cannot be dismissed while the artists are being merged
    useEffect(() => {
        modals.updateContextModal({
            modalId: id,
            closeOnEscape: !merging,
            closeOnClickOutside: !merging,
            withCloseButton: !merging,
        });
    }, [id, merging]);

    // Artist names are not unique, so each option also shows the artist's album and song counts
    const renderOption = useCallback((artist: MergeableArtist) => (
        <Group gap="sm" wrap="nowrap" h="100%">
            <Artwork id={artist.photo} size={ARTIST_ARTWORK_SIZE} placeholderIcon={<IconUser size={20}/>}
                     enablePreview={false}/>
            <Stack gap={0} style={{minWidth: 0}}>
                <Text size="sm" lineClamp={1}>{artist.name}</Text>
                <Text size="xs" c="dimmed" lineClamp={1}>
                    {t("common:count.albums", {count: artist.albumsCount ?? 0})}
                    {", "}
                    {t("common:count.songs", {count: artist.songsCount ?? 0})}
                </Text>
            </Stack>
        </Group>
    ), [t]);

    const handleMerge = async () => {
        setError(null);
        setMerging(true);
        try {
            await mergeArtists(target.id, sourceIds);
        } catch (error) {
            setMerging(false);
            setError((error instanceof ApiProblemError ? error.detail : undefined) ?? t("artists:merge.failed"));
            return;
        }

        context.closeModal(id);
        onSuccess?.();
    };

    // The server explains why the artists cannot be merged (e.g. the placeholder artist merged into another one)
    const shownError = error ?? (previewQuery.error
        ? (previewQuery.error instanceof ApiProblemError ? previewQuery.error.detail : undefined)
            ?? t("artists:merge.previewFailed")
        : null);

    return (
        <Stack data-testid="artist-merge" data-loading={previewQuery.isFetching ? "true" : "false"}>
            <Text c="dimmed" size="sm">
                {t("artists:merge.instructions")}
            </Text>
            <VirtualSelect
                items={artists}
                value={target}
                onChange={artist => {
                    setTarget(artist);
                    setError(null);
                }}
                getKey={getArtistKey}
                getLabel={getArtistLabel}
                renderOption={renderOption}
                optionHeight={ARTIST_OPTION_HEIGHT}
                emptyMessage={t("artists:merge.empty")}
                label={t("artists:merge.targetLabel")}
                disabled={merging}
                testId="artist-merge-target"
            />
            {preview && !shownError && (
                <Alert color="orange" icon={<IconAlertTriangle/>} data-testid="artist-merge-summary">
                    <Stack gap={4}>
                        <Text size="sm">
                            {t("artists:merge.summary", {count: preview.songsCount, name: target.name})}
                        </Text>
                        {preview.mergedAlbumsCount > 0 && (
                            <Text size="sm" data-testid="artist-merge-merged-albums">
                                {t("artists:merge.mergedAlbums", {count: preview.mergedAlbumsCount})}
                            </Text>
                        )}
                        <Text size="sm">
                            {t("artists:merge.deleted", {count: sourceIds.length})}
                        </Text>
                    </Stack>
                </Alert>
            )}
            {shownError && (
                <Alert color="red" data-testid="artist-merge-error">
                    {shownError}
                </Alert>
            )}
            <Group justify="flex-end">
                <Button variant="subtle" onClick={() => context.closeModal(id)} disabled={merging}>
                    {t("common:actions.cancel")}
                </Button>
                <Button
                    leftSection={<IconArrowMerge size={16}/>}
                    onClick={handleMerge}
                    loading={merging}
                    disabled={!preview || previewQuery.isFetching || previewQuery.isError}
                    data-testid="artist-merge-confirm"
                >
                    {t("artists:merge.confirm")}
                </Button>
            </Group>
        </Stack>
    );
}
