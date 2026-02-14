import {ActionIcon, Alert, Anchor, Box, Button, Flex, Group, Stack, Text} from "@mantine/core";
import {
    IconArrowBack,
    IconArrowForward,
    IconArrowRightDashed,
    IconDisc,
    IconDownload,
    IconHeart,
    IconHeartFilled,
    IconMusic,
    IconPlayerPlayFilled,
    IconPlaylistAdd,
    IconTag,
    IconUser
} from "@tabler/icons-react";
import {Link, useParams} from "@tanstack/react-router";
import {saveAs} from 'file-saver';

import {getDownloadSongUrl, useGetSong} from "../../client/songs.ts";
import {usePlayerActions} from "../../contexts/player-context.tsx";
import Artwork from "../common/artwork.tsx";
import ExplicitLabel from "../common/explicit-label.tsx";

export default function SongDetailPage() {
    const {songId} = useParams({from: '/songs/$songId'});
    const {data: response} = useGetSong(Number(songId));
    const song = response?.data.song;
    const playerActions = usePlayerActions();

    if (!song) {
        return <Box p="md">Loading...</Box>;
    }

    return (
        <Stack gap="md">
            <Link to="/songs">
                <Group gap="xs">
                    <IconArrowBack size={16}/>
                    <Text size="sm">Back to Songs</Text>
                </Group>
            </Link>

            <Flex gap="xl" align="flex-start">
                <Artwork
                    id={song.cover}
                    size={200}
                    placeholderIcon={<IconMusic size={80}/>}
                />
                <Stack gap="xs" style={{flex: 1}}>
                    <Text size="xl" fw={700}>{song.title}</Text>
                    <Group gap="xs">
                        <IconUser size={16}/>
                        {song.artists.map(artist => (
                            <Anchor key={artist.id} component={Link} to={`/artists/${artist.id}`} c="blue"
                                    size="sm">{artist.name}</Anchor>
                        ))}
                    </Group>
                    <Group gap="md">
                        <Group gap="xs">
                            <IconDisc size={16}/>
                            <Anchor component={Link} to={`/albums/${song.album.id}`} c="blue"
                                    size="sm">{song.album.name}</Anchor>
                        </Group>
                        {song.year && <Text size="sm" c="dimmed">{song.year}</Text>}
                        <Text size="sm" c="dimmed">{song.duration}</Text>
                        {song.isExplicit &&
                            <ExplicitLabel visible={true}><Text size="sm">Explicit</Text></ExplicitLabel>}
                    </Group>
                    <Group gap="xs">
                        <IconTag size={16}/>
                        {song.genres.length > 0 ? (
                            song.genres.map(genre => (
                                <Text key={genre.id} size="sm" c="dimmed">{genre.name}</Text>
                            ))
                        ) : (
                            <Text size="sm" c="dimmed">No genres</Text>
                        )}
                    </Group>
                    <Group gap="sm">
                        <Button leftSection={<IconPlayerPlayFilled/>} onClick={() => playerActions.play([song])}>
                            Play
                        </Button>
                        <Group gap="xs">
                            <ActionIcon variant="outline" size="lg" onClick={() => playerActions.playNext([song])}
                                        title="Play Next">
                                <IconArrowRightDashed/>
                            </ActionIcon>
                            <ActionIcon variant="outline" size="lg" onClick={() => playerActions.playLast([song])}
                                        title="Play Last">
                                <IconArrowForward/>
                            </ActionIcon>
                        </Group>
                        <Button
                            leftSection={song.isFavorite ? <IconHeartFilled/> : <IconHeart/>}
                            variant={song.isFavorite ? "filled" : "default"}
                        >
                            {song.isFavorite ? "Unfavorite" : "Favorite"}
                        </Button>
                        <Button leftSection={<IconPlaylistAdd/>} variant="default">
                            Add to Playlist
                        </Button>
                        <Button
                            leftSection={<IconDownload/>}
                            variant="default"
                            onClick={() => saveAs(getDownloadSongUrl(song.id))}
                        >
                            Download
                        </Button>
                    </Group>
                </Stack>
            </Flex>

            {song.lyrics ? (
                <Box>
                    <Text size="lg" fw={600} mb="sm">Lyrics</Text>
                    <Text style={{whiteSpace: 'pre-wrap'}}>{song.lyrics}</Text>
                </Box>
            ) : (
                <Alert color="gray" title="Lyrics not found">
                    Lyrics are not available for this song.
                </Alert>
            )}
        </Stack>
    );
}
