import {useQuery} from "@tanstack/react-query";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useArtistsSchema} from "./useArtistsSchema.tsx";

export default function ArtistsPage() {
    const {t} = useTranslation(["artists", "common"]);
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
                throw new Error(t("artists:page.fetchFailed"));
            }

            return response.json();
        },
    });

    const artists = useQueryData(artistsQuery, t("artists:page.fetchFailed")) ?? {artists: []};

    const artistsSchema = useArtistsSchema();

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = artists?.artists ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}} data-testid="artists">
            <Collection
                key="artists"
                stateKey="artists"
                items={elements}
                schema={artistsSchema}
                isFetching={artistsQuery.isFetching}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder={t("artists:page.searchPlaceholder")}
            />
        </div>
    );
}
