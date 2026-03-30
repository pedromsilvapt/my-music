import {Button, Group, Stack, Text} from '@mantine/core';
import type {ContextModalProps} from '@mantine/modals';
import {useDeleteQueue} from '../../client/playlists.ts';

interface DeleteQueueModalInnerProps {
    queueId: number;
    queueName: string;
    onSuccess?: () => void;
}

export default function DeleteQueueModal({
    context,
    id,
    innerProps,
}: ContextModalProps<DeleteQueueModalInnerProps>) {
    const deleteQueue = useDeleteQueue({
        mutation: {
            onSuccess: () => {
                context.closeModal(id);
                innerProps.onSuccess?.();
            }
        }
    });

    const handleDelete = () => {
        deleteQueue.mutate({id: innerProps.queueId});
    };

    return (
        <Stack>
            <Text>
                Are you sure you want to delete "{innerProps.queueName}"? This action cannot be undone.
            </Text>
            <Group justify="flex-end">
                <Button variant="subtle" onClick={() => context.closeModal(id)}>
                    Cancel
                </Button>
                <Button color="red" onClick={handleDelete} loading={deleteQueue.isPending}>
                    Delete
                </Button>
            </Group>
        </Stack>
    );
}
