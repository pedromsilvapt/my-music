import {useEffect} from "react";
import {useListArtists} from "../../client/artists.ts";
import Collection from "../common/collection/collection.tsx";
import {useArtistsSchema} from "./useArtistsSchema.tsx";

export default function ArtistsPage() {
    const {data: artists, refetch} = useListArtists();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const artistsSchema = useArtistsSchema();

    const elements = artists?.data?.artists ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="artists"
                items={elements}
                schema={artistsSchema}>
            </Collection>
        </div>
    </>;
}
