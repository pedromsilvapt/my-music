import {useParams} from "@tanstack/react-router";
import {useEffect, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useGetPlaylist} from "../../client/playlists.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";

export default function PlaylistDetailPage() {
    const {t} = useTranslation(["playlists", "common"]);
    const {playlistId} = useParams({from: '/playlists/$playlistId'});
    const id = parseInt(playlistId, 10);
    const playlistQuery = useGetPlaylist(id);
    const playlistResponse = useQueryData(playlistQuery, t("playlists:detailPage.fetchFailed"));

    const queueContext = useMemo(() => ({
        type: 'playlist' as const,
        playlistName: playlistResponse?.data?.playlist?.name,
    }), [playlistResponse?.data?.playlist?.name]);

    const songsSchema = useSongsSchema(false, {queueContext});

    const refetch = playlistQuery.refetch;

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch();
    }, [refetch]);

    const elements = playlistResponse?.data?.playlist?.songs ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key={`playlist-${id}`}
                stateKey="playlist-detail"
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    );
}
