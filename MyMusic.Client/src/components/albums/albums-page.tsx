import {useQuery} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import Collection from "../common/collection/collection.tsx";
import {useAlbumsSchema} from "./useAlbumsSchema.tsx";

export default function AlbumsPage() {
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const {data, refetch} = useQuery({
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

    const albumsSchema = useAlbumsSchema();

    useEffect(() => {
        void refetch();
    }, [refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = data?.albums ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="albums"
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
