import {useEffect} from "react";
import {useListAlbums} from "../../client/albums.ts";
import Collection from "../common/collection/collection.tsx";
import {useAlbumsSchema} from "./useAlbumsSchema.tsx";

export default function AlbumsPage() {
    const {data: albums, refetch} = useListAlbums();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch()
    }, [refetch]);

    const albumsSchema = useAlbumsSchema();

    const elements = albums?.data?.albums ?? [];

    return <>
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="artists"
                items={elements}
                schema={albumsSchema}>
            </Collection>
        </div>
    </>;
}
