import {useQuery} from "@tanstack/react-query";
import {useEffect} from "react";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSongsResponse} from "../../model";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "./useSongsSchema.tsx";

const SONGS_STATE_KEY = "songs";

export default function SongsPage() {
    const {registerRefetch, unregisterRefetch} = useManagePlaylistsContext();
    const {setCollectionServerSearch, setCollectionServerFilter} = useCollectionActions(state => ({
        setCollectionServerSearch: state.setCollectionServerSearch,
        setCollectionServerFilter: state.setCollectionServerFilter,
    }));
    const collectionState = useCollectionStateByKey(SONGS_STATE_KEY);
    const appliedSearch = collectionState.serverSearch;
    const appliedFilter = collectionState.serverFilter;

    const songsQuery = useQuery({
        queryKey: ["songs", appliedSearch, appliedFilter],
        queryFn: async (): Promise<ListSongsResponse> => {
            const params = new URLSearchParams();
            if (appliedSearch) params.set("search", appliedSearch);
            if (appliedFilter) params.set("filter", appliedFilter);

            const url = `/api/songs${params.toString() ? `?${params.toString()}` : ""}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error("Failed to fetch songs");
            }

            return response.json();
        },
    });

    const songs = useQueryData(songsQuery, "Failed to fetch songs") ?? {songs: []};

    const songsSchema = useSongsSchema();

    useEffect(() => {
        registerRefetch('songs', songsQuery.refetch);
        return () => unregisterRefetch('songs');
    }, [registerRefetch, unregisterRefetch, songsQuery.refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setCollectionServerSearch(SONGS_STATE_KEY, newSearch);
        setCollectionServerFilter(SONGS_STATE_KEY, newFilter);
    };

    const elements = songs?.songs ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key={SONGS_STATE_KEY}
                stateKey={SONGS_STATE_KEY}
                items={elements}
                schema={songsSchema}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder="Search songs..."
            />
        </div>
    );
}
