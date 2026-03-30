import {Button, Group, Stack, TextInput} from '@mantine/core';
import type {ContextModalProps} from '@mantine/modals';
import {useState} from 'react';
import {useRenameQueue} from '../../client/playlists.ts';

interface RenameQueueModalInnerProps {
    queueId: number;
    queueName: string;
    onSuccess?: () => void;
}

export default function RenameQueueModal({
    context,
    id,
    innerProps,
}: ContextModalProps<RenameQueueModalInnerProps>) {
    const [name, setName] = useState(innerProps.queueName);

    const renameQueue = useRenameQueue({
        mutation: {
            onSuccess: () => {
                context.closeModal(id);
                innerProps.onSuccess?.();
            }
        }
    });

    const handleRename = () => {
        if (name.trim()) {
            renameQueue.mutate({id: innerProps.queueId, data: {name: name.trim()}});
        }
    };

    return (
        <Stack>
            <TextInput
                label="Queue Name"
                placeholder="Enter queue name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                        handleRename();
                    }
                }}
                autoFocus
            />
            <Group justify="flex-end">
                <Button variant="subtle" onClick={() => context.closeModal(id)}>
                    Cancel
                </Button>
                <Button onClick={handleRename} loading={renameQueue.isPending}>
                    Save
                </Button>
            </Group>
        </Stack>
    );
}
