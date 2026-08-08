import {Button, Group, Title} from "@mantine/core";
import {IconPlus} from "@tabler/icons-react";
import {useQuery} from "@tanstack/react-query";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import Collection from "../common/collection/collection.tsx";
import CreatePlaylistModal from "./create-playlist-modal.tsx";
import {usePlaylistsSchema} from "./usePlaylistsSchema.tsx";

const PLAYLISTS_STATE_KEY = "playlists";

export default function PlaylistsPage() {
    const {t} = useTranslation(["playlists", "common"]);
    const [opened, setOpened] = useState(false);
    const {setCollectionFilter} = useCollectionActions(state => ({
        setCollectionFilter: state.setCollectionFilter,
    }));
    const collectionState = useCollectionStateByKey(PLAYLISTS_STATE_KEY);
    const appliedSearch = collectionState.filter.search;
    const appliedFilter = collectionState.filter.expression;

    const playlistsQuery = useQuery({
        queryKey: ["playlists", appliedSearch, appliedFilter],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (appliedSearch) params.set("search", appliedSearch);
            if (appliedFilter) params.set("filter", appliedFilter);

            const url = `/api/playlists${params.toString() ? `?${params.toString()}` : ""}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error(t("playlists:page.fetchFailed"));
            }

            return response.json();
        },
    });

    const playlists = useQueryData(playlistsQuery, t("playlists:page.fetchFailed")) ?? {playlists: []};

    const playlistsSchema = usePlaylistsSchema();

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setCollectionFilter(PLAYLISTS_STATE_KEY, { search: newSearch, expression: newFilter });
    };

    const elements = playlists?.playlists ?? [];

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}} data-testid="playlists">
            <Group justify="space-between" mb="md">
                <Title order={2}>{t("playlists:page.title")}</Title>
                <Button leftSection={<IconPlus size={16}/>} onClick={() => setOpened(true)}>
                    {t("playlists:page.createPlaylist")}
                </Button>
            </Group>

            <CreatePlaylistModal
                opened={opened}
                onClose={() => setOpened(false)}
                onSuccess={() => playlistsQuery.refetch()}
            />

            <div style={{flex: 1, minHeight: 0}}>
                <Collection
                    items={elements}
                    schema={playlistsSchema}
                    initialView="grid"
                    stateKey={PLAYLISTS_STATE_KEY}
                    isFetching={playlistsQuery.isFetching}
                    filterMode="server"
                    serverSearch={appliedSearch}
                    serverFilter={appliedFilter}
                    onServerFilterChange={handleFilterChange}
                    searchPlaceholder={t("playlists:page.searchPlaceholder")}
                />
            </div>
        </div>
    );
}
