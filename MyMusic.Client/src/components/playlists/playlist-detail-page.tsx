import {useParams} from "@tanstack/react-router";
import {useEffect} from "react";
import {useGetPlaylist} from "../../client/playlists.ts";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";

export default function PlaylistDetailPage() {
    const {playlistId} = useParams({from: '/playlists/$playlistId'});
    const id = parseInt(playlistId, 10);
    const {data: playlist, refetch} = useGetPlaylist(id);

    const songsSchema = useSongsSchema();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch();
    }, [refetch]);

    const elements = playlist?.data?.playlist?.songs ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key={`playlist-${id}`}
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    );
}
