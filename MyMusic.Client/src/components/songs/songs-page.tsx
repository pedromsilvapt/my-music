import {useEffect} from "react";
import {useListSongs} from '../../client/songs.ts';
import {usePlayerContext} from "../../contexts/player-context.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "./useSongsSchema.tsx";

export default function SongsPage() {
    const playerStore = usePlayerContext();

    const {data: songs, refetch} = useListSongs();

    const songsSchema = useSongsSchema(playerStore);
    
    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const elements = songs?.data?.songs ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
                items={elements}
                schema={songsSchema}>
            </Collection>
        </div>
    </>;
}
