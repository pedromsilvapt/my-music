import {Button} from "@mantine/core";
import {IconTrash} from '@tabler/icons-react';
import {useTranslation} from "react-i18next";

interface SoundalikeToolbarProps {
    selectedGroupsCount: number;
    readyToResolve: boolean;
    onRemoveDuplicates: () => void;
}

export default function SoundalikeToolbar({selectedGroupsCount, readyToResolve, onRemoveDuplicates}: SoundalikeToolbarProps) {
    const {t} = useTranslation(["audits", "common"]);
    return (
        <Button
            leftSection={<IconTrash size={16}/>}
            onClick={onRemoveDuplicates}
            disabled={!readyToResolve}
            color="red"
        >
            {t("audits:soundalike.toolbar.removeDuplicates", {count: selectedGroupsCount})}
        </Button>
    );
}
