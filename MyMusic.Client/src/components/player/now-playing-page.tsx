import {Stack, Text} from "@mantine/core";
import {useMemo} from "react";
import {useCurrentSong, useQueue, useQueueMutations} from "../../contexts/player-context";
import Collection from "../common/collection/collection";
import {useSongsSchema} from "../songs/useSongsSchema";

export default function NowPlayingPage() {
    const {reorder, reorderBatch} = useQueueMutations();
    const {queue} = useQueue();
    const current = useCurrentSong();

    const songsSchema = useSongsSchema(true);

    const currentSongIndex = useMemo(() => {
        return current ? queue.findIndex(s => s.id === current.id) : -1;
    }, [queue, current]);

    const handleReorder = (fromIndex: number, toIndex: number) => {
        reorder(fromIndex, toIndex);
    };

    const handleReorderBatch = (reorders: { fromIndex: number; toIndex: number }[]) => {
        reorderBatch(reorders);
    };

    return (
        <Stack gap="md">
            <Text size="xl" fw={700}>Now Playing ({currentSongIndex + 1}/{queue.length} songs)</Text>
            <Collection
                items={queue}
                schema={songsSchema}
                sortable={true}
                onReorder={handleReorder}
                onReorderBatch={handleReorderBatch}
            />
        </Stack>
    );
}
