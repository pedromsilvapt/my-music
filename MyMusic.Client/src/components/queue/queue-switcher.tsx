import {ActionIcon, Group, Stack, Text} from '@mantine/core';
import {modals} from '@mantine/modals';
import {IconPencil, IconPlaylist, IconTrash} from '@tabler/icons-react';
import type {ListQueueItem} from '../../model';
import {PlayingDot} from './playing-dot';

interface QueueSwitcherProps {
    queues: ListQueueItem[];
    visibleQueueId: number | null;
    currentQueueId: number | null;
    onViewQueue: (queueId: number) => void;
    onClosePopover?: () => void;
}

export function QueueSwitcher({queues, visibleQueueId, currentQueueId, onViewQueue, onClosePopover}: QueueSwitcherProps) {
    const handleOpenRename = (queue: ListQueueItem) => {
        onClosePopover?.();
        modals.openContextModal({
            modal: 'rename-queue',
            title: 'Rename Queue',
            innerProps: {
                queueId: queue.id,
                queueName: queue.name,
            },
        });
    };

    const handleOpenDelete = (queue: ListQueueItem) => {
        onClosePopover?.();
        modals.openContextModal({
            modal: 'delete-queue',
            title: 'Delete Queue',
            innerProps: {
                queueId: queue.id,
                queueName: queue.name,
            },
        });
    };

    return (
        <Stack gap="xs">
            {queues.length === 0 ? (
                <Text size="sm" c="dimmed" ta="center" py="md">
                    No queues yet. Click Play to create one.
                </Text>
            ) : (
                queues.map(queue => {
                    const isViewing = queue.id === visibleQueueId;
                    const isPlaying = queue.id === currentQueueId;
                    return (
                        <Group key={queue.id} justify="space-between" wrap="nowrap">
                            <Group
                                gap="xs"
                                style={{
                                    cursor: 'pointer',
                                    flex: 1,
                                    minWidth: 0,
                                }}
                                onClick={() => queue.id !== visibleQueueId && onViewQueue(queue.id)}
                            >
                                <IconPlaylist
                                    size={16}
                                    style={{
                                        opacity: isViewing ? 1 : 0.5,
                                    }}
                                />
                                {isPlaying && <PlayingDot />}
                                <Text
                                    size="sm"
                                    fw={isViewing ? 600 : 400}
                                    style={{
                                        overflow: 'hidden',
                                        textOverflow: 'ellipsis',
                                        whiteSpace: 'nowrap',
                                    }}
                                >
                                    {queue.name}
                                </Text>
                                <Text size="xs" c="dimmed">
                                    {queue.songCount} {queue.songCount === 1 ? 'song' : 'songs'}
                                </Text>
                            </Group>

                            <Group gap={4}>
                                <ActionIcon
                                    variant="subtle"
                                    size="sm"
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        handleOpenRename(queue);
                                    }}
                                >
                                    <IconPencil size={14}/>
                                </ActionIcon>
                                <ActionIcon
                                    variant="subtle"
                                    size="sm"
                                    color="red"
                                    onClick={(e) => {
                                        e.stopPropagation();
                                        handleOpenDelete(queue);
                                    }}
                                >
                                    <IconTrash size={14}/>
                                </ActionIcon>
                            </Group>
                        </Group>
                    );
                })
            )}
        </Stack>
    );
}
