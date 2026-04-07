import {Button} from "@mantine/core";
import {IconTrash} from '@tabler/icons-react';

interface SoundalikeToolbarProps {
    selectedGroupsCount: number;
    readyToResolve: boolean;
    onRemoveDuplicates: () => void;
}

export default function SoundalikeToolbar({selectedGroupsCount, readyToResolve, onRemoveDuplicates}: SoundalikeToolbarProps) {
    return (
        <Button
            leftSection={<IconTrash size={16}/>}
            onClick={onRemoveDuplicates}
            disabled={!readyToResolve}
            color="red"
        >
            Remove Duplicates ({selectedGroupsCount})
        </Button>
    );
}
