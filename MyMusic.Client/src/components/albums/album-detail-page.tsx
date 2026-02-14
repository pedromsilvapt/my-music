import {Anchor, Box, Flex, Group, Stack, Text} from "@mantine/core";
import {IconArrowBack, IconDisc} from "@tabler/icons-react";
import {Link, useParams} from "@tanstack/react-router";
import {useGetAlbum} from "../../client/albums.ts";
import {usePlayerContext} from "../../contexts/player-context.tsx";
import type {ListSongsItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";

export default function AlbumDetailPage() {
    const {albumId} = useParams({from: '/albums/$albumId'});
    const {data: response} = useGetAlbum(Number(albumId));
    const album = response?.data.album;
    const playerStore = usePlayerContext();
    const songsSchema = useSongsSchema(playerStore);

    if (!album) {
        return <Box p="md">Loading...</Box>;
    }

    const songs = album.songs as unknown as ListSongsItem[];

    return (
        <Stack gap="md">
            <Link to="/albums">
                <Group gap="xs">
                    <IconArrowBack size={16}/>
                    <Text size="sm">Back to Albums</Text>
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
                    <Anchor component={Link} to={`/artists/${album.artistId}`} size="sm">{album.artistName}</Anchor>
                    <Group gap="md">
                        {album.year && <Text size="sm" c="dimmed">{album.year}</Text>}
                        <Text size="sm" c="dimmed">{album.songsCount} songs</Text>
                    </Group>
                </Stack>
            </Flex>

            <Box>
                <Collection
                    items={songs}
                    schema={songsSchema}
                />
            </Box>
        </Stack>
    );
}
