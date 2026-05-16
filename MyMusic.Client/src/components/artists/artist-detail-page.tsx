import {Box, Flex, Group, SegmentedControl, Stack, Text} from "@mantine/core";
import {IconArrowBack, IconUser} from "@tabler/icons-react";
import {Link, useNavigate, useParams, useSearch} from "@tanstack/react-router";
import {useGetArtist} from "../../client/artists.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListAlbumItem, ListSongItem} from "../../model";
import {ArtistSongFilter} from "../../model";
import {useAlbumsSchema} from "../albums/useAlbumsSchema.tsx";
import Artwork from "../common/artwork.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";
import {useMemo} from "react";

export default function ArtistDetailPage() {
    const {artistId} = useParams({from: '/artists/$artistId'});
    const search = useSearch({from: '/artists/$artistId'});
    const navigate = useNavigate({from: '/artists/$artistId'});
    const searchParams = search as Record<string, unknown>;
    const songFilter = (searchParams.songFilter as string)?.toLowerCase() ?? 'all';

    const artistQuery = useGetArtist(Number(artistId), {
        songFilter: songFilter === 'own' ? ArtistSongFilter.Own : songFilter === 'other' ? ArtistSongFilter.Other : ArtistSongFilter.All
    });
    const artistResponse = useQueryData(artistQuery, "Failed to fetch artist");
    const artist = artistResponse?.data.artist ?? null;

    const albumsSchema = useAlbumsSchema();

    const queueContext = useMemo(() => ({
        type: 'artist' as const,
        artistName: artist?.name,
    }), [artist?.name]);

    const songsSchema = useSongsSchema(false, {queueContext});

    if (!artist) {
        return <Box p="md" data-testid="artist-detail" data-loading="true">Loading...</Box>;
    }

    const albums = artist.albums as unknown as ListAlbumItem[];
    const songs = artist.songs as unknown as ListSongItem[];

    return (
        <Stack gap="md" data-testid="artist-detail" data-loading={artistQuery.isFetching ? "true" : "false"}>
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
                    initialView="grid"
                    stateKey="artist-albums"
                    items={albums}
                    schema={albumsSchema}
                    autoHeight
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
                    stateKey="artist-songs"
                    items={songs}
                    schema={songsSchema}
                    autoHeight
                />
            </Box>
        </Stack>
    );
}
