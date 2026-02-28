import {useQuery} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useArtistsSchema} from "./useArtistsSchema.tsx";

export default function ArtistsPage() {
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const artistsQuery = useQuery({
        queryKey: ["artists", appliedSearch, appliedFilter],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (appliedSearch) params.set("search", appliedSearch);
            if (appliedFilter) params.set("filter", appliedFilter);

            const url = `/api/artists${params.toString() ? `?${params.toString()}` : ""}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error("Failed to fetch artists");
            }

            return response.json();
        },
    });

    const artists = useQueryData(artistsQuery, "Failed to fetch artists") ?? {artists: []};

    const artistsSchema = useArtistsSchema();

    useEffect(() => {
        void artistsQuery.refetch();
    }, [artistsQuery.refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = artists?.artists ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="artists"
                stateKey="artists"
                items={elements}
                schema={artistsSchema}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder="Search artists..."
            />
        </div>
    );
}
