import {useQueryClient} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useListSongs} from "../../client/songs.ts";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "./useSongsSchema.tsx";
import SongImportDropzone from "./song-import-dropzone.tsx";
import SongImportProgress from "./song-import-progress.tsx";

const SONGS_STATE_KEY = "songs";

export default function SongsPage() {
    const queryClient = useQueryClient();
    const {registerRefetch, unregisterRefetch} = useManagePlaylistsContext();
    const {setCollectionFilter} = useCollectionActions(state => ({
        setCollectionFilter: state.setCollectionFilter,
    }));
    const collectionState = useCollectionStateByKey(SONGS_STATE_KEY);
    const appliedSearch = collectionState.filter.search;
    const appliedFilter = collectionState.filter.expression;

    const [importFiles, setImportFiles] = useState<File[]>([]);
    const [showImportProgress, setShowImportProgress] = useState(false);

    const songsQuery = useListSongs(
        { search: appliedSearch, filter: appliedFilter },
        { 
            query: { 
                enabled: true,
                select: (response) => response.data
            } 
        }
    );

    const songs = useQueryData(songsQuery, "Failed to fetch songs") ?? {songs: []};

    const songsSchema = useSongsSchema();

    useEffect(() => {
        registerRefetch('songs', songsQuery.refetch);
        return () => unregisterRefetch('songs');
    }, [registerRefetch, unregisterRefetch, songsQuery.refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setCollectionFilter(SONGS_STATE_KEY, { search: newSearch, expression: newFilter });
    };

    const handleFilesDropped = (files: File[]) => {
        setImportFiles(files);
        setShowImportProgress(true);
    };

    const handleImportClose = () => {
        setShowImportProgress(false);
        setImportFiles([]);
        queryClient.invalidateQueries({queryKey: ["api", "songs"]});
    };

    const elements = songs?.songs ?? [];

    return (
        <SongImportDropzone onFilesDropped={handleFilesDropped}>
            <div style={{height: 'var(--parent-height)', position: 'relative'}} data-testid="songs">
                <Collection
                    key={SONGS_STATE_KEY}
                    stateKey={SONGS_STATE_KEY}
                    items={elements}
                    schema={songsSchema}
                    isFetching={songsQuery.isFetching}
                    filterMode="server"
                    serverSearch={appliedSearch}
                    serverFilter={appliedFilter}
                    onServerFilterChange={handleFilterChange}
                    searchPlaceholder="Search songs..."
                />
                <SongImportProgress
                    opened={showImportProgress}
                    onClose={handleImportClose}
                    files={importFiles}
                />
            </div>
        </SongImportDropzone>
    );
}
