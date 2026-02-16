import {useEffect} from "react";
import {useListSongs} from '../../client/songs.ts';
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "./useSongsSchema.tsx";

export default function SongsPage() {
    const {registerRefetch, unregisterRefetch} = useManagePlaylistsContext();

    const {data: songs, refetch} = useListSongs();

    const songsSchema = useSongsSchema();

    useEffect(() => {
        registerRefetch('songs', refetch);
        return () => unregisterRefetch('songs');
    }, [registerRefetch, unregisterRefetch, refetch]);

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const elements = songs?.data?.songs ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    );
}
