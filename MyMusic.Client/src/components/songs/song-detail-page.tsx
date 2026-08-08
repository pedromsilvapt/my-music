import {ActionIcon, Alert, Anchor, Box, Button, Flex, Group, Stack, Text, Tooltip} from "@mantine/core";
import {
    IconArrowBack,
    IconArrowForward,
    IconArrowRightDashed,
    IconDevicesCog,
    IconDisc,
    IconDownload,
    IconEdit,
    IconFile,
    IconHeart,
    IconHeartFilled,
    IconMusic,
    IconPlayerPlayFilled,
    IconPlaylistAdd,
    IconShare,
    IconTag,
    IconTrash,
    IconUser
} from "@tabler/icons-react";
import {useCallback} from "react";
import {Link, useNavigate, useParams} from "@tanstack/react-router";
import {saveAs} from 'file-saver';
import {useTranslation} from "react-i18next";

import {getDownloadSongUrl, useDeleteSongs, useGetLocalSong} from "../../client/songs.ts";
import {modals} from '@mantine/modals';
import {SONG_EDITOR_MODAL_SIZE} from "../../consts.ts";
import {useManageDevicesContext} from "../../contexts/manage-devices-context.tsx";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {useManageSharingContext} from "../../contexts/manage-sharing-context.tsx";
import {useQueueMutations} from "../../contexts/player-context.tsx";
import {useToggleFavorite} from "../../hooks/use-favorites.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {formatFileSize} from "../../utils/format-file-size.ts";
import {formatRelativeDate} from "../../utils/format-relative-date.ts";
import Artwork from "../common/artwork.tsx";
import ExplicitLabel from "../common/explicit-label.tsx";
import DeviceBadge from "../devices/device-badge.tsx";

export default function SongDetailPage() {
    const {t} = useTranslation(["songs", "common"]);
    const {songId} = useParams({from: '/songs/$songId'});
    const navigate = useNavigate();
    const songQuery = useGetLocalSong(Number(songId));
    const songResponse = useQueryData(songQuery, t("songs:detailPage.fetchFailed"));
    const song = songResponse?.data.song ?? null;
    const {play, playNext, playLast} = useQueueMutations();
    const toggleFavorite = useToggleFavorite();
    const {open: openManagePlaylists} = useManagePlaylistsContext();
    const {open: openManageDevices} = useManageDevicesContext();
    const {open: openManageSharing} = useManageSharingContext();
    const deleteSongs = useDeleteSongs();

    const handleDelete = useCallback(() => {
        if (!song) return;
        modals.openConfirmModal({
            title: t("songs:detailPage.deleteTitle"),
            children: (
                <Text size="sm">
                    {t("songs:detailPage.deleteConfirm", {name: song.title})}
                </Text>
            ),
            labels: {confirm: t("common:actions.delete"), cancel: t("common:actions.cancel")},
            confirmProps: {color: 'red'},
            onConfirm: () => {
                deleteSongs.mutate({data: {songIds: [song.id]}}, {
                    onSuccess: () => {
                        navigate({to: '/songs'});
                    },
                });
            },
        });
    }, [song, deleteSongs, navigate, t]);

    if (!song) {
        return <Box p="md" data-testid="song-detail" data-loading="true">{t("common:common.loading")}</Box>;
    }

    return (
        <Stack gap="md"  data-testid="song-detail" data-loading={songQuery.isFetching ? "true" : "false"}>
            <Link to="/songs">
                <Group gap="xs">
                    <IconArrowBack size={16}/>
                    <Text size="sm">{t("songs:detailPage.backToSongs")}</Text>
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
                            <ExplicitLabel visible={true}><Text size="sm">{t("songs:detailPage.explicit")}</Text></ExplicitLabel>}
                    </Group>
                    <Group gap="xs" data-testid="song-genres">
                        <IconTag size={16}/>
                        {song.genres.length > 0 ? (
                            song.genres.map(genre => (
                                <Text key={genre.id} size="sm" c="dimmed" data-testid="genre-item">{genre.name}</Text>
                            ))
                        ) : (
                            <Text size="sm" c="dimmed">{t("songs:detailPage.noGenres")}</Text>
                        )}
                    </Group>
                    {song.repositoryPath && (
                        <Group gap="xs">
                            <IconFile size={16}/>
                            <Text size="sm" c="dimmed" data-testid="song-repository-path">{song.repositoryPath}</Text>
                        </Group>
                    )}
                    <Group gap="xs" data-testid="song-devices">
                        {song.devices.length > 0 ? (
                             song.devices.map(device => (
                                 <DeviceBadge
                                     key={device.songDeviceId}
                                     name={device.name}
                                     icon={device.icon}
                                     color={device.color}
                                     syncAction={device.syncAction}
                                 />
                             ))
                        ) : (
                            <Text size="sm" c="dimmed">{t("songs:detailPage.noDevices")}</Text>
                        )}
                    </Group>
                    <Group gap="sm">
                        <Button leftSection={<IconPlayerPlayFilled/>} onClick={() => play([{...song, isShared: false}])}>
                            {t("songs:schema.play")}
                        </Button>
                        <Group gap="xs">
                            <ActionIcon variant="outline" size="lg" onClick={() => playNext([{...song, isShared: false}])}
                                        title={t("songs:schema.playNext")}>
                                <IconArrowRightDashed/>
                            </ActionIcon>
                            <ActionIcon variant="outline" size="lg" onClick={() => playLast([{...song, isShared: false}])}
                                        title={t("songs:schema.playLast")}>
                                <IconArrowForward/>
                            </ActionIcon>
                        </Group>
                        <Button
                            leftSection={song.isFavorite ? <IconHeartFilled/> : <IconHeart/>}
                            variant={song.isFavorite ? "filled" : "default"}
                            onClick={() => toggleFavorite.mutate({id: song.id})}
                        >
                            {song.isFavorite ? t("songs:schema.unfavorite") : t("songs:schema.favorite")}
                        </Button>
                        <Button
                            leftSection={<IconEdit/>}
                            variant="default"
                            onClick={() => modals.openContextModal({
                                modal: 'song-editor',
                                title: t("songs:detailPage.editTitle"),
                                size: SONG_EDITOR_MODAL_SIZE,
                                innerProps: { songIds: [song.id] },
                            })}
                        >
                            {t("common:actions.edit")}
                        </Button>
                        <Button
                            leftSection={<IconPlaylistAdd/>}
                            variant="default"
                            onClick={() => openManagePlaylists([song.id])}
                        >
                            {t("songs:schema.managePlaylists")}
                        </Button>
                        <Button
                            leftSection={<IconDevicesCog/>}
                            variant="default"
                            onClick={() => openManageDevices([song.id])}
                        >
                            {t("songs:schema.manageDevices")}
                        </Button>
                        {!song.isShared && (
                            <Button
                                leftSection={<IconShare/>}
                                variant="default"
                                onClick={() => openManageSharing([song.id])}
                            >
                                {t("songs:schema.manageSharing")}
                            </Button>
                        )}
                        <Button
                            leftSection={<IconDownload/>}
                            variant="default"
                            onClick={() => saveAs(getDownloadSongUrl(song.id))}
                        >
                            {t("songs:schema.download")}
                        </Button>
                        <Button
                            leftSection={<IconTrash/>}
                            variant="default"
                            color="red"
                            onClick={handleDelete}
                        >
                            {t("common:actions.delete")}
                        </Button>
                    </Group>
                </Stack>
            </Flex>

            <Box>
                <Text size="lg" fw={600} mb="sm">{t("songs:detailPage.lyrics")}</Text>
                {song.lyrics
                    ? <Text style={{whiteSpace: 'pre-wrap'}}>{song.lyrics}</Text>
                    : <Alert color="gray" title={t("songs:detailPage.lyricsNotFound")}/>
                }
            </Box>
        </Stack>
    );
}
