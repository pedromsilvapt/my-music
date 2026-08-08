import {Text, Group} from "@mantine/core";
import {useQueryClient} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {useManageSharingContext} from "../../contexts/manage-sharing-context.tsx";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useListSongs} from "../../client/songs.ts";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "./useSongsSchema.tsx";
import SongImportDropzone from "./song-import-dropzone.tsx";
import SongImportProgress from "./song-import-progress.tsx";

const SONGS_STATE_KEY = "songs";

export interface SongsPageProps {
    ownerId?: number;
    sharerName?: string;
}

export default function SongsPage({ownerId, sharerName}: SongsPageProps) {
    const queryClient = useQueryClient();
    const {registerRefetch, unregisterRefetch} = useManagePlaylistsContext();
    const {registerRefetch: registerSharingRefetch, unregisterRefetch: unregisterSharingRefetch} = useManageSharingContext();
    const {setCollectionFilter} = useCollectionActions(state => ({
        setCollectionFilter: state.setCollectionFilter,
    }));
    const collectionState = useCollectionStateByKey(SONGS_STATE_KEY);
    const appliedSearch = collectionState.filter.search;
    const appliedFilter = collectionState.filter.expression;

    const [importFiles, setImportFiles] = useState<File[]>([]);
    const [showImportProgress, setShowImportProgress] = useState(false);

    const isSharedView = ownerId !== undefined;

    const songsQuery = useListSongs(
        {ownerId, search: appliedSearch, filter: appliedFilter},
        {
            query: {
                enabled: true,
                select: (response) => response.data,
            },
        },
    );

    const songs = useQueryData(songsQuery, "Failed to fetch songs") ?? {songs: []};

    const songsSchema = useSongsSchema();

    useEffect(() => {
        registerRefetch('songs', songsQuery.refetch);
        return () => unregisterRefetch('songs');
    }, [registerRefetch, unregisterRefetch, songsQuery.refetch]);

    useEffect(() => {
        registerSharingRefetch('songs', songsQuery.refetch);
        return () => unregisterSharingRefetch('songs');
    }, [registerSharingRefetch, unregisterSharingRefetch, songsQuery.refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setCollectionFilter(SONGS_STATE_KEY, {search: newSearch, expression: newFilter});
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
    const pageTitle = isSharedView ? `Shared by ${sharerName ?? "Unknown"}` : undefined;

    const content = (
        <div
            style={{height: 'var(--parent-height)', position: 'relative'}}
            data-testid={isSharedView ? "shared-songs" : "songs"}
            data-loading={songsQuery.isFetching ? "true" : "false"}
        >
            {pageTitle && (
                <Group justify="space-between" mb="sm">
                    <Text fw={600} size="lg">{pageTitle}</Text>
                </Group>
            )}
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
            {!isSharedView && (
                <SongImportProgress
                    opened={showImportProgress}
                    onClose={handleImportClose}
                    files={importFiles}
                />
            )}
        </div>
    );

    if (isSharedView) {
        return content;
    }

    return (
        <SongImportDropzone onFilesDropped={handleFilesDropped}>
            {content}
        </SongImportDropzone>
    );
}
