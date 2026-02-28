import {Button, Group, Title} from "@mantine/core";
import {IconPlus} from "@tabler/icons-react";
import {useQuery} from "@tanstack/react-query";
import {useEffect, useState} from "react";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import CreatePlaylistModal from "./create-playlist-modal.tsx";
import {usePlaylistsSchema} from "./usePlaylistsSchema.tsx";

export default function PlaylistsPage() {
    const [opened, setOpened] = useState(false);
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const playlistsQuery = useQuery({
        queryKey: ["playlists", appliedSearch, appliedFilter],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (appliedSearch) params.set("search", appliedSearch);
            if (appliedFilter) params.set("filter", appliedFilter);

            const url = `/api/playlists${params.toString() ? `?${params.toString()}` : ""}`;
            const response = await fetch(url);

            if (!response.ok) {
                throw new Error("Failed to fetch playlists");
            }

            return response.json();
        },
    });

    const playlists = useQueryData(playlistsQuery, "Failed to fetch playlists") ?? {playlists: []};

    const playlistsSchema = usePlaylistsSchema();

    useEffect(() => {
        void playlistsQuery.refetch();
    }, [playlistsQuery.refetch]);

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = playlists?.playlists ?? [];

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <Group justify="space-between" mb="md">
                <Title order={2}>Playlists</Title>
                <Button leftSection={<IconPlus size={16}/>} onClick={() => setOpened(true)}>
                    Create Playlist
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
                    stateKey="playlists"
                    filterMode="server"
                    serverSearch={appliedSearch}
                    serverFilter={appliedFilter}
                    onServerFilterChange={handleFilterChange}
                    searchPlaceholder="Search playlists..."
                />
            </div>
        </div>
    );
}
