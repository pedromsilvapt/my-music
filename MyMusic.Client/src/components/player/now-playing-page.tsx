import {Stack, Text} from "@mantine/core";
import {useMemo} from "react";
import {usePlayerActions, usePlayerContext} from "../../contexts/player-context.tsx";
import Collection from "../common/collection/collection.tsx";
import {useSongsSchema} from "../songs/useSongsSchema.tsx";

export default function NowPlayingPage() {
    const playerActions = usePlayerActions();
    const queue = usePlayerContext(state => state.queue);
    const current = useCurrentSong();

    const songsSchema = useSongsSchema(playerActions, true, current?.id);

    const currentSongIndex = useMemo(() => {
        return current ? queue.indexOf(current) : -1;
    }, [queue, current]);

    return (
        <Stack gap="md">
            <Text size="xl" fw={700}>Now Playing ({currentSongIndex + 1}/{queue.length} songs)</Text>
            <Collection
                items={queue}
                schema={songsSchema}
            />
        </Stack>
    );
}

export function useCurrentSong() {
    return usePlayerContext(state => state.current.type === 'LOADING' || state.current.type === 'LOADED' ? state.current.song : null);
}
