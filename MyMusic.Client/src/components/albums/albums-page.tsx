import {useQuery} from "@tanstack/react-query";
import {useEffect} from "react";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import Collection from "../common/collection/collection.tsx";
import {useAlbumsSchema} from "./useAlbumsSchema.tsx";

const ALBUMS_STATE_KEY = "albums";

export default function AlbumsPage() {
    const {setCollectionServerSearch, setCollectionServerFilter} = useCollectionActions(state => ({
        setCollectionServerSearch: state.setCollectionServerSearch,
        setCollectionServerFilter: state.setCollectionServerFilter,
    }));
    const collectionState = useCollectionStateByKey(ALBUMS_STATE_KEY);
    const appliedSearch = collectionState.serverSearch;
    const appliedFilter = collectionState.serverFilter;

    const albumsQuery = useQuery({
        queryKey: ["albums", appliedSearch, appliedFilter],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (appliedSearch) params.set("search", appliedSearch);
            if (appliedFilter) params.set("filter", appliedFilter);

            const url = `/api/albums${params.toString() ? `?${params.toString()}` : ""}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error("Failed to fetch albums");
            }

            return response.json();
        },
    });

    const albums = useQueryData(albumsQuery, "Failed to fetch albums") ?? {albums: []};

    const albumsSchema = useAlbumsSchema();

    useEffect(() => {
        void albumsQuery.refetch();
    }, [albumsQuery.refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setCollectionServerSearch(ALBUMS_STATE_KEY, newSearch);
        setCollectionServerFilter(ALBUMS_STATE_KEY, newFilter);
    };

    const elements = albums?.albums ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key={ALBUMS_STATE_KEY}
                stateKey={ALBUMS_STATE_KEY}
                items={elements}
                schema={albumsSchema}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder="Search albums..."
            />
        </div>
    );
}
