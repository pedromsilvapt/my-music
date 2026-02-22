import {useQuery} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import type {ListSongsResponse} from "../../model";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "./useSongsSchema.tsx";

export default function SongsPage() {
    const {registerRefetch, unregisterRefetch} = useManagePlaylistsContext();
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const {data: songs, refetch} = useQuery({
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

    const songsSchema = useSongsSchema();

    useEffect(() => {
        registerRefetch('songs', refetch);
        return () => unregisterRefetch('songs');
    }, [registerRefetch, unregisterRefetch, refetch]);

    const handleFilterChange = (search: string, filter: string) => {
        setAppliedSearch(search);
        setAppliedFilter(filter);
    };

    const elements = songs?.songs ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="songs"
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
