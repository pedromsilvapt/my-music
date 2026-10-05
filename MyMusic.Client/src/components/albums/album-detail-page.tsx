import {Anchor, Box, Button, Flex, Group, Stack, Text} from "@mantine/core";
import {IconArrowBack, IconDisc, IconTrash} from "@tabler/icons-react";
import {Link, useNavigate, useParams} from "@tanstack/react-router";
import {useGetAlbum} from "../../client/albums.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSongItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";
import {useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useConfirmDeleteAlbums} from "./use-confirm-delete-albums.tsx";

export default function AlbumDetailPage() {
    const {t} = useTranslation(["albums", "common"]);
    const {albumId} = useParams({from: '/albums/$albumId'});
    const albumQuery = useGetAlbum(Number(albumId));
    const albumResponse = useQueryData(albumQuery, t("albums:detail.fetchFailed"));
    const album = albumResponse?.data.album ?? null;

    const queueContext = useMemo(() => ({
        type: 'album' as const,
        albumName: album?.name,
    }), [album?.name]);

    const songsSchema = useSongsSchema(false, {queueContext});

    const navigate = useNavigate();
    const confirmDeleteAlbums = useConfirmDeleteAlbums();

    if (!album) {
        return <Box p="md" data-testid="album-detail" data-loading="true">{t("albums:detail.loading")}</Box>;
    }

    const songs = album.songs as unknown as ListSongItem[];

    return (
        <Stack gap="md" data-testid="album-detail" data-loading={albumQuery.isFetching ? "true" : "false"}>
            <Link to="/albums">
                <Group gap="xs">
                    <IconArrowBack size={16}/>
                    <Text size="sm">{t("albums:detail.backToAlbums")}</Text>
                </Group>
            </Link>

            <Flex gap="xl" align="flex-start">
                <Artwork
                    id={album.cover}
                    size={200}
                    placeholderIcon={<IconDisc size={80}/>}
                />
                <Stack gap="xs">
                    <Text size="xl" fw={700}>{album.name}</Text>
                    <Anchor component={Link} to={`/artists/${album.artistId}`} size="sm" data-testid="album-artist">{album.artistName}</Anchor>
                    <Group gap="md">
                        {album.year && <Text size="sm" c="dimmed">{album.year}</Text>}
                        <Text size="sm" c="dimmed" data-testid="album-songs-count" data-count={album.songsCount}>{t("albums:detail.songsCount", {count: album.songsCount})}</Text>
                    </Group>
                    {!album.isShared && (
                        <Group gap="xs" mt="md">
                            <Button
                                leftSection={<IconTrash/>}
                                variant="default"
                                color="red"
                                onClick={() => confirmDeleteAlbums([album], () => navigate({to: '/albums'}))}
                                data-testid="album-delete"
                            >
                                {t("common:actions.delete")}
                            </Button>
                        </Group>
                    )}
                </Stack>
            </Flex>

            <Box>
                <Collection
                    stateKey="album-detail"
                    items={songs}
                    schema={songsSchema}
                    autoHeight
                />
            </Box>
        </Stack>
    );
}
