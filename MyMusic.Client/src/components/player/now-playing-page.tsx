import {Stack, Text} from "@mantine/core";
import {useMemo, useState, useEffect, useRef} from "react";
import {useCurrentSong, useQueue, useQueueMutations} from "../../contexts/player-context";
import {usePlaybackStore} from "../../stores/playback-store";
import Collection from "../common/collection/collection";
import {useSongsSchema} from "../songs/useSongsSchema";

export default function NowPlayingPage() {
    const {reorder, reorderBatch} = useQueueMutations();
    const {queue} = useQueue();
    const current = useCurrentSong();
    const scrollToCurrentRequestId = usePlaybackStore(s => s.scrollToCurrentRequestId);

    const songsSchema = useSongsSchema(true);

    const currentSongIndex = useMemo(() => {
        return current ? queue.findIndex(s => s.id === current.id) : -1;
    }, [queue, current]);

    const [scrollToIndex, setScrollToIndex] = useState<number | undefined>();
    const [highlightRequestId, setHighlightRequestId] = useState<number | undefined>();
    const prevScrollRequestIdRef = useRef(scrollToCurrentRequestId);

    useEffect(() => {
        if (scrollToCurrentRequestId !== prevScrollRequestIdRef.current && currentSongIndex >= 0) {
            prevScrollRequestIdRef.current = scrollToCurrentRequestId;
            setScrollToIndex(currentSongIndex);
            setHighlightRequestId(scrollToCurrentRequestId);
        }
    }, [scrollToCurrentRequestId, currentSongIndex]);

    useEffect(() => {
        if (currentSongIndex >= 0 && currentSongIndex !== scrollToIndex) {
            setScrollToIndex(currentSongIndex);
        }
    }, [currentSongIndex, scrollToIndex]);

    const handleReorder = (fromIndex: number, toIndex: number) => {
        reorder(fromIndex, toIndex);
    };

    const handleReorderBatch = (reorders: { fromIndex: number; toIndex: number }[]) => {
        reorderBatch(reorders);
    };

    console.log(scrollToIndex)
    return (
        <Stack gap="md" style={{height: 'var(--parent-height)'}}>
            <Text size="xl" fw={700}>Now Playing ({currentSongIndex + 1}/{queue.length} songs)</Text>
            <Collection
                stateKey="now-playing"
                items={queue}
                schema={songsSchema}
                sortable={true}
                onReorder={handleReorder}
                onReorderBatch={handleReorderBatch}
                scrollToIndex={scrollToIndex}
                highlightRequestId={highlightRequestId}
            />
        </Stack>
    );
}
