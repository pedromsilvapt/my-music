import {Alert, Button, Group, Stack, Text} from "@mantine/core";
import {type ContextModalProps, modals} from "@mantine/modals";
import {IconAlertTriangle, IconArrowMerge, IconDisc} from "@tabler/icons-react";
import {useCallback, useEffect, useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import {ALBUM_PLACEHOLDER_NAME} from "../../consts.ts";
import {useAlbumsMergePreview} from "../../hooks/use-albums-merge-preview.ts";
import {useMergeAlbumsWithInvalidation} from "../../hooks/use-merge-albums.ts";
import {ApiProblemError} from "../../utils/api-problem.ts";
import Artwork from "../common/artwork.tsx";
import VirtualSelect from "../common/virtual-select.tsx";

const ALBUM_OPTION_HEIGHT = 56;
const ALBUM_ARTWORK_SIZE = 40;

export interface MergeableAlbum {
    id: number;
    name: string;
    cover?: number | null;
    year?: number | null;
    songsCount?: number | null;
}

export interface AlbumMergeModalInnerProps {
    albums: MergeableAlbum[];
    onSuccess?: () => void;
}

const getAlbumKey = (album: MergeableAlbum) => album.id;
const getAlbumLabel = (album: MergeableAlbum) => album.name;

/**
 * The album kept by default: a placeholder album, which cannot be merged into another one, or else the album
 * with the most songs, so the fewest files are rewritten.
 */
const getDefaultTarget = (albums: MergeableAlbum[]) =>
    albums.find(album => album.name === ALBUM_PLACEHOLDER_NAME)
    ?? albums.reduce((best, album) => (album.songsCount ?? 0) > (best.songsCount ?? 0) ? album : best);

/**
 * Merges the albums picked by the user into one of them: the user chooses the album to keep, and the others
 * are merged into it as a single operation. The dialog says what the merge changes before it is confirmed,
 * stays open until the server is done rewriting the songs, and shows why when it refuses.
 */
export default function AlbumMergeModal({context, id, innerProps}: ContextModalProps<AlbumMergeModalInnerProps>) {
    const {t} = useTranslation(["albums", "common"]);
    const {albums, onSuccess} = innerProps;
    const [target, setTarget] = useState(() => getDefaultTarget(albums));
    const [error, setError] = useState<string | null>(null);
    const [merging, setMerging] = useState(false);
    const mergeAlbums = useMergeAlbumsWithInvalidation();

    const sourceIds = useMemo(
        () => albums.filter(album => album.id !== target.id).map(album => album.id),
        [albums, target]);
    const previewQuery = useAlbumsMergePreview(target.id, sourceIds);
    const preview = previewQuery.data;

    // The dialog cannot be dismissed while the albums are being merged
    useEffect(() => {
        modals.updateContextModal({
            modalId: id,
            closeOnEscape: !merging,
            closeOnClickOutside: !merging,
            withCloseButton: !merging,
        });
    }, [id, merging]);

    const renderOption = useCallback((album: MergeableAlbum) => (
        <Group gap="sm" wrap="nowrap" h="100%">
            <Artwork id={album.cover} size={ALBUM_ARTWORK_SIZE} placeholderIcon={<IconDisc size={20}/>}
                     enablePreview={false}/>
            <Stack gap={0} style={{minWidth: 0}}>
                <Text size="sm" lineClamp={1}>{album.name}</Text>
                <Text size="xs" c="dimmed" lineClamp={1}>
                    {album.year != null && `${album.year}, `}
                    {t("common:count.songs", {count: album.songsCount ?? 0})}
                </Text>
            </Stack>
        </Group>
    ), [t]);

    const handleMerge = async () => {
        setError(null);
        setMerging(true);
        try {
            await mergeAlbums(target.id, sourceIds);
        } catch (error) {
            setMerging(false);
            setError((error instanceof ApiProblemError ? error.detail : undefined) ?? t("albums:merge.failed"));
            return;
        }

        context.closeModal(id);
        onSuccess?.();
    };

    // The server explains why the albums cannot be merged (e.g. a placeholder album merged into another one)
    const shownError = error ?? (previewQuery.error
        ? (previewQuery.error instanceof ApiProblemError ? previewQuery.error.detail : undefined)
            ?? t("albums:merge.previewFailed")
        : null);

    return (
        <Stack data-testid="album-merge" data-loading={previewQuery.isFetching ? "true" : "false"}>
            <Text c="dimmed" size="sm">
                {t("albums:merge.instructions")}
            </Text>
            <VirtualSelect
                items={albums}
                value={target}
                onChange={album => {
                    setTarget(album);
                    setError(null);
                }}
                getKey={getAlbumKey}
                getLabel={getAlbumLabel}
                renderOption={renderOption}
                optionHeight={ALBUM_OPTION_HEIGHT}
                emptyMessage={t("albums:merge.empty")}
                label={t("albums:merge.targetLabel")}
                disabled={merging}
                testId="album-merge-target"
            />
            {preview && !shownError && (
                <Alert color="orange" icon={<IconAlertTriangle/>} data-testid="album-merge-summary">
                    <Stack gap={4}>
                        <Text size="sm">
                            {t("albums:merge.summary", {
                                count: preview.songsCount,
                                name: target.name,
                                artist: preview.targetArtistName,
                            })}
                        </Text>
                        {preview.songsGainingArtistCount > 0 && (
                            <Text size="sm" data-testid="album-merge-gaining-artist">
                                {t("albums:merge.gainingArtist", {
                                    count: preview.songsGainingArtistCount,
                                    artist: preview.targetArtistName,
                                })}
                            </Text>
                        )}
                        <Text size="sm">
                            {t("albums:merge.deleted", {count: sourceIds.length})}
                        </Text>
                    </Stack>
                </Alert>
            )}
            {shownError && (
                <Alert color="red" data-testid="album-merge-error">
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
                    data-testid="album-merge-confirm"
                >
                    {t("albums:merge.confirm")}
                </Button>
            </Group>
        </Stack>
    );
}
