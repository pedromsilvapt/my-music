import {Button, Group, Text, Title} from "@mantine/core";
import {IconShare} from "@tabler/icons-react";
import {useParams} from "@tanstack/react-router";
import {useEffect, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useGetPlaylist} from "../../client/playlists.ts";
import {useManageSharingContext} from "../../contexts/manage-sharing-context.tsx";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";
import PlaylistShareIndicator from "./playlist-share-indicator.tsx";

export default function PlaylistDetailPage() {
    const {t} = useTranslation(["playlists", "common"]);
    const {playlistId} = useParams({from: '/playlists/$playlistId'});
    const id = parseInt(playlistId, 10);
    const playlistQuery = useGetPlaylist(id);
    const playlistResponse = useQueryData(playlistQuery, t("playlists:detailPage.fetchFailed"));
    const playlist = playlistResponse?.data?.playlist;
    const {open: openManageSharing, registerRefetch, unregisterRefetch} = useManageSharingContext();

    const queueContext = useMemo(() => ({
        type: 'playlist' as const,
        playlistName: playlist?.name,
    }), [playlist?.name]);

    const songsSchema = useSongsSchema(false, {queueContext});

    const refetch = playlistQuery.refetch;

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch();
    }, [refetch]);

    useEffect(() => {
        const key = `playlist-detail-${id}`;
        registerRefetch(key, refetch);
        return () => unregisterRefetch(key);
    }, [id, refetch, registerRefetch, unregisterRefetch]);

    const elements = playlist?.songs ?? [];

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}
             data-testid="playlist-detail"
             data-loading={playlistQuery.isFetching ? "true" : "false"}>
            {playlist && (
                <Group justify="space-between" mb="md" wrap="nowrap">
                    <Group gap="xs" wrap="nowrap" style={{minWidth: 0}}>
                        <Title order={2} style={{overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap'}}>
                            {playlist.name}
                        </Title>
                        <PlaylistShareIndicator playlist={playlist} size={20}/>
                        {playlist.isSharedWithMe && (
                            <Text c="dimmed" size="sm" data-testid="playlist-shared-by">
                                {t("playlists:share.sharedBy", {name: playlist.ownerName})}
                            </Text>
                        )}
                    </Group>
                    {!playlist.isSharedWithMe && playlist.type === "Playlist" && (
                        <Button variant="default" leftSection={<IconShare size={16}/>}
                                onClick={() => openManageSharing([playlist.id])}>
                            {t("playlists:detailPage.share")}
                        </Button>
                    )}
                </Group>
            )}
            <div style={{flex: 1, minHeight: 0}}>
                <Collection
                    key={`playlist-${id}`}
                    stateKey="playlist-detail"
                    items={elements}
                    schema={songsSchema}>
                </Collection>
            </div>
        </div>
    );
}
