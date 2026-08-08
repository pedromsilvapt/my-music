import {Button, Group, Stack, Text} from '@mantine/core';
import type {ContextModalProps} from '@mantine/modals';
import {useTranslation} from 'react-i18next';
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
    const {t} = useTranslation(["queue", "common"]);
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
                {t("queue:deleteModal.confirm", {name: innerProps.queueName})}
            </Text>
            <Group justify="flex-end">
                <Button variant="subtle" onClick={() => context.closeModal(id)}>
                    {t("common:actions.cancel")}
                </Button>
                <Button color="red" onClick={handleDelete} loading={deleteQueue.isPending}>
                    {t("common:actions.delete")}
                </Button>
            </Group>
        </Stack>
    );
}
