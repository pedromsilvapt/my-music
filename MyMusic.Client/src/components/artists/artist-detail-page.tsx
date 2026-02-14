import {Box, Flex, Group, SegmentedControl, Stack, Text} from "@mantine/core";
import {IconArrowBack, IconUser} from "@tabler/icons-react";
import {Link, useNavigate, useParams, useSearch} from "@tanstack/react-router";
import {useGetArtist} from "../../client/artists.ts";
import {usePlayerContext} from "../../contexts/player-context.tsx";
import type {ListAlbumsItem, ListSongsItem} from "../../model";
import {GetArtistSongFilter} from "../../model";
import {useAlbumsSchema} from "../albums/useAlbumsSchema.tsx";
import Artwork from "../common/artwork.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";

export default function ArtistDetailPage() {
    const {artistId} = useParams({from: '/artists/$artistId'});
    const search = useSearch({from: '/artists/$artistId'});
    const navigate = useNavigate({from: '/artists/$artistId'});
    const searchParams = search as Record<string, unknown>;
    const songFilter = (searchParams.songFilter as string)?.toLowerCase() ?? 'all';
    const playerStore = usePlayerContext();
    const albumsSchema = useAlbumsSchema();
    const songsSchema = useSongsSchema(playerStore);

    const {data: response} = useGetArtist(Number(artistId), {songFilter: songFilter === 'own' ? GetArtistSongFilter.Own : songFilter === 'other' ? GetArtistSongFilter.Other : GetArtistSongFilter.All});
    const artist = response?.data.artist;

    if (!artist) {
        return <Box p="md">Loading...</Box>;
    }

    const albums = artist.albums as unknown as ListAlbumsItem[];
    const songs = artist.songs as unknown as ListSongsItem[];

    return (
        <Stack gap="md">
            <Link to="/artists">
                <Group gap="xs">
                    <IconArrowBack size={16}/>
                    <Text size="sm">Back to Artists</Text>
                </Group>
            </Link>

            <Flex gap="xl" align="flex-start">
                <Artwork
                    id={artist.photo}
                    size={200}
                    placeholderIcon={<IconUser size={80}/>}
                />
                <Stack gap="xs">
                    <Text size="xl" fw={700}>{artist.name}</Text>
                    <Group gap="md">
                        <Text size="sm" c="dimmed">{artist.albumsCount} albums</Text>
                        <Text size="sm" c="dimmed">{artist.songsCount} songs</Text>
                    </Group>
                </Stack>
            </Flex>

            <Box>
                <Text size="lg" fw={600} mb="sm">Albums</Text>
                <Collection
                    items={albums}
                    schema={albumsSchema}
                />
            </Box>

            <Box>
                <Group justify="space-between" mb="sm">
                    <Text size="lg" fw={600}>Songs</Text>
                    <SegmentedControl
                        size="xs"
                        value={songFilter}
                        onChange={(value) => {
                            navigate({search: {songFilter: value as 'all' | 'own' | 'other'}});
                        }}
                        data={[
                            {label: 'All', value: 'all'},
                            {label: 'Own Albums', value: 'own'},
                            {label: 'Other Albums', value: 'other'},
                        ]}
                    />
                </Group>
                <Collection
                    items={songs}
                    schema={songsSchema}
                />
            </Box>
        </Stack>
    );
}
