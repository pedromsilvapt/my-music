import {ActionIcon, Alert, Anchor, Box, Button, Flex, Group, Stack, Text, Tooltip} from "@mantine/core";
import {
    IconArrowBack,
    IconArrowForward,
    IconArrowRightDashed,
    IconDevicesCog,
    IconDisc,
    IconDownload,
    IconEdit,
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

import {getDownloadSongUrl, useGetLocalSong} from "../../client/songs.ts";
import {modals} from '@mantine/modals';
import {SONG_EDITOR_MODAL_SIZE} from "../../consts.ts";
import {useManageDevicesContext} from "../../contexts/manage-devices-context.tsx";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {useQueueMutations} from "../../contexts/player-context.tsx";
import {useToggleFavorite} from "../../hooks/use-favorites.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {formatFileSize} from "../../utils/format-file-size.ts";
import {formatRelativeDate} from "../../utils/format-relative-date.ts";
import Artwork from "../common/artwork.tsx";
import ExplicitLabel from "../common/explicit-label.tsx";
import DeviceBadge from "../devices/device-badge.tsx";

export default function SongDetailPage() {
    const {songId} = useParams({from: '/songs/$songId'});
    const songQuery = useGetLocalSong(Number(songId));
    const songResponse = useQueryData(songQuery, "Failed to fetch song");
    const song = songResponse?.data.song ?? null;
    const {play, playNext, playLast} = useQueueMutations();
    const toggleFavorite = useToggleFavorite();
    const {open: openManagePlaylists} = useManagePlaylistsContext();
    const {open: openManageDevices} = useManageDevicesContext();

    if (!song) {
        return <Box p="md" data-testid="song-detail" data-loading="true">Loading...</Box>;
    }

    return (
        <Stack gap="md"  data-testid="song-detail" data-loading={songQuery.isFetching ? "true" : "false"}>
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
                        {song.year && <Text size="sm" c="dimmed" data-testid="song-year">{song.year}</Text>}
                        <Text size="sm" c="dimmed">{song.duration}</Text>
                        {song.bitrate && <Text size="sm" c="dimmed">{song.bitrate} kbps</Text>}
                        {song.size > 0 && <Text size="sm" c="dimmed">{formatFileSize(song.size)}</Text>}
                        {song.createdAt && (
                            <Tooltip label={new Date(song.createdAt).toLocaleString()} openDelay={500}>
                                <Text size="sm" c="dimmed">{formatRelativeDate(song.createdAt)}</Text>
                            </Tooltip>
                        )}
                        {song.isExplicit &&
                            <ExplicitLabel visible={true}><Text size="sm">Explicit</Text></ExplicitLabel>}
                    </Group>
                    <Group gap="xs" data-testid="song-genres">
                        <IconTag size={16}/>
                        {song.genres.length > 0 ? (
                            song.genres.map(genre => (
                                <Text key={genre.id} size="sm" c="dimmed" data-testid="genre-item">{genre.name}</Text>
                            ))
                        ) : (
                            <Text size="sm" c="dimmed">No genres</Text>
                        )}
                    </Group>
                    <Group gap="xs">
                        {song.devices.length > 0 ? (
                            song.devices.map(device => (
                                <DeviceBadge
                                    key={device.id}
                                    name={device.name}
                                    icon={device.icon}
                                    color={device.color}
                                    syncAction={device.syncAction}
                                />
                            ))
                        ) : (
                            <Text size="sm" c="dimmed">No devices</Text>
                        )}
                    </Group>
                    <Group gap="sm">
                        <Button leftSection={<IconPlayerPlayFilled/>} onClick={() => play([song])}>
                            Play
                        </Button>
                        <Group gap="xs">
                            <ActionIcon variant="outline" size="lg" onClick={() => playNext([song])}
                                        title="Play Next">
                                <IconArrowRightDashed/>
                            </ActionIcon>
                            <ActionIcon variant="outline" size="lg" onClick={() => playLast([song])}
                                        title="Play Last">
                                <IconArrowForward/>
                            </ActionIcon>
                        </Group>
                        <Button
                            leftSection={song.isFavorite ? <IconHeartFilled/> : <IconHeart/>}
                            variant={song.isFavorite ? "filled" : "default"}
                            onClick={() => toggleFavorite.mutate({id: song.id})}
                        >
                            {song.isFavorite ? "Unfavorite" : "Favorite"}
                        </Button>
                        <Button
                            leftSection={<IconEdit/>}
                            variant="default"
                            onClick={() => modals.openContextModal({
                                modal: 'song-editor',
                                title: 'Edit Song',
                                size: SONG_EDITOR_MODAL_SIZE,
                                innerProps: { songIds: [song.id] },
                            })}
                        >
                            Edit
                        </Button>
                        <Button
                            leftSection={<IconPlaylistAdd/>}
                            variant="default"
                            onClick={() => openManagePlaylists([song.id])}
                        >
                            Manage Playlists
                        </Button>
                        <Button
                            leftSection={<IconDevicesCog/>}
                            variant="default"
                            onClick={() => openManageDevices([song.id])}
                        >
                            Manage Devices
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

            <Box>
                <Text size="lg" fw={600} mb="sm">Lyrics</Text>
                {song.lyrics
                    ? <Text style={{whiteSpace: 'pre-wrap'}}>{song.lyrics}</Text>
                    : <Alert color="gray" title="Lyrics not found on this song"/>
                }
            </Box>
        </Stack>
    );
}
