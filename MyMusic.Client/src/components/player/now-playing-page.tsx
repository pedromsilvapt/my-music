import {Group, Popover, Stack, Text} from "@mantine/core";
import {IconChevronDown} from "@tabler/icons-react";
import {useMemo, useState, useEffect, useRef} from "react";
import {useQueueMutations} from "../../contexts/player-context";
import {useVisibleQueue} from "../../hooks/use-visible-queue";
import {usePlaybackStore} from "../../stores/playback-store";
import {useQueuesMutations, useQueueList} from "../../hooks/use-queues";
import Collection from "../common/collection/collection";
import {useSongsSchema} from "../songs/useSongsSchema";
import {QueueSwitcher} from "../queue/queue-switcher";
import {PlayingDot} from "../queue/playing-dot";
import {useDisclosure} from "@mantine/hooks";

export default function NowPlayingPage() {
    const {reorder, reorderBatch} = useQueueMutations();
    const {queue, currentSongId: visibleQueueCurrentSongId, queueId} = useVisibleQueue();
    const scrollToCurrentRequestId = usePlaybackStore((s: { scrollToCurrentRequestId: number }) => s.scrollToCurrentRequestId);
    const {viewQueue} = useQueuesMutations();
    const {queues, visibleQueueId, currentQueueId} = useQueueList();

    const songsSchema = useSongsSchema(true, {visibleQueueId, currentQueueId, visibleQueueCurrentSongId});

    const visibleQueueCurrentSongIndex = useMemo(() => {
        return visibleQueueCurrentSongId != null ? queue.findIndex(s => s.id === visibleQueueCurrentSongId) : -1;
    }, [queue, visibleQueueCurrentSongId]);

    const [scrollToIndex, setScrollToIndex] = useState<number | undefined>();
    const [highlightRequestId, setHighlightRequestId] = useState<number | undefined>();
    const [popoverOpened, {open: openPopover, close: closePopover, toggle: togglePopover}] = useDisclosure(false);
    const prevScrollRequestIdRef = useRef(scrollToCurrentRequestId);
    const prevQueueIdRef = useRef(queueId);

    useEffect(() => {
        if (queueId !== prevQueueIdRef.current) {
            prevQueueIdRef.current = queueId;
            if (visibleQueueCurrentSongIndex >= 0) {
                setScrollToIndex(visibleQueueCurrentSongIndex);
            }
        }
    }, [queueId, visibleQueueCurrentSongIndex]);

    useEffect(() => {
        if (scrollToCurrentRequestId !== prevScrollRequestIdRef.current && visibleQueueCurrentSongIndex >= 0) {
            prevScrollRequestIdRef.current = scrollToCurrentRequestId;
            setScrollToIndex(visibleQueueCurrentSongIndex);
            setHighlightRequestId(scrollToCurrentRequestId);
        }
    }, [scrollToCurrentRequestId, visibleQueueCurrentSongIndex]);

    const handleReorder = (fromIndex: number, toIndex: number) => {
        reorder(fromIndex, toIndex);
    };

    const handleReorderBatch = (reorders: { fromIndex: number; toIndex: number }[]) => {
        reorderBatch(reorders);
    };

    const handleViewQueue = (queueId: number) => {
        viewQueue(queueId);
        closePopover();
    };

    const visibleQueue = queues.find(q => q.id === visibleQueueId);
    const queueName = visibleQueue?.name ?? 'Now Playing';

    return (
        <Stack gap="md" style={{height: 'var(--parent-height)'}}>
            <Group justify="space-between" wrap="nowrap">
                <Popover
                    position="bottom-start"
                    shadow="md"
                    opened={popoverOpened}
                    onChange={(open) => open ? openPopover() : closePopover()}
                    disabled={queues.length <= 1}
                >
                    <Popover.Target>
                        <Group
                            gap={4}
                            style={{cursor: queues.length > 1 ? 'pointer' : 'default'}}
                            onClick={() => queues.length > 1 && togglePopover()}
                        >
                            <Group gap={6}>
                                {visibleQueueId === currentQueueId && <PlayingDot />}
                                <Text size="xl" fw={700}>
                                    {queueName}
                                </Text>
                                <Text size="sm" c="dimmed">
                                    {queue.length} {queue.length === 1 ? 'song' : 'songs'}
                                </Text>
                            </Group>
                            {queues.length > 1 && (
                                <IconChevronDown size={18} style={{opacity: 0.6}}/>
                            )}
                        </Group>
                    </Popover.Target>
                    <Popover.Dropdown>
                        <QueueSwitcher
                            queues={queues}
                            visibleQueueId={visibleQueueId}
                            currentQueueId={currentQueueId}
                            onViewQueue={handleViewQueue}
                            onClosePopover={closePopover}
                        />
                    </Popover.Dropdown>
                </Popover>
            </Group>

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
