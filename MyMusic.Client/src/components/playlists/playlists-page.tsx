import {Button, Group, Title} from "@mantine/core";
import {IconPlus} from "@tabler/icons-react";
import {useEffect, useState} from "react";
import {useListPlaylists} from "../../client/playlists.ts";
import Collection from "../common/collection/collection.tsx";
import CreatePlaylistModal from "./create-playlist-modal.tsx";
import {usePlaylistsSchema} from "./usePlaylistsSchema.tsx";

export default function PlaylistsPage() {
    const [opened, setOpened] = useState(false);

    const {data: playlists, refetch} = useListPlaylists();
    const playlistsSchema = usePlaylistsSchema();

    useEffect(() => {
        // noinspection JSIgnoredPromiseFromCall
        refetch();
    }, [refetch]);

    const elements = playlists?.data?.playlists ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Group justify="space-between" mb="md">
                <Title order={2}>Playlists</Title>
                <Button leftSection={<IconPlus size={16}/>} onClick={() => setOpened(true)}>
                    Create Playlist
                </Button>
            </Group>

            <CreatePlaylistModal
                opened={opened}
                onClose={() => setOpened(false)}
                onSuccess={() => refetch()}
            />

            <Collection
                items={elements}
                schema={playlistsSchema}
                initialView="grid"
            />
        </div>
    );
}
