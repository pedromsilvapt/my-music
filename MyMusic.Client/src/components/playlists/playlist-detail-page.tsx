import {useParams} from "@tanstack/react-router";
import {useEffect} from "react";
import {useGetPlaylist} from "../../client/playlists.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";

export default function PlaylistDetailPage() {
    const {playlistId} = useParams({from: '/playlists/$playlistId'});
    const id = parseInt(playlistId, 10);
    const playlistQuery = useGetPlaylist(id);
    const playlistResponse = useQueryData(playlistQuery, "Failed to fetch playlist");

    const songsSchema = useSongsSchema();

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
