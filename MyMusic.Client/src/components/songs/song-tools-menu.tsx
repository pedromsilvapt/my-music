import {ActionIcon, Menu} from "@mantine/core";
import {IconHash, IconTool} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";
import {useRecalculateSongChecksumWithNotifications} from "../../hooks/use-recalculate-song-checksum.ts";

interface SongToolsMenuProps {
    songId: number;
    disabled?: boolean;
}

/**
 * Toolbox of maintenance commands to run on a song. Each tool runs in the background and reports its outcome
 * in a notification.
 */
export default function SongToolsMenu({songId, disabled}: SongToolsMenuProps) {
    const {t} = useTranslation(["songs", "common"]);
    const {recalculate, isPending: isRecalculatePending} = useRecalculateSongChecksumWithNotifications();

    return (
        <Menu shadow="md" position="top-start" withinPortal>
            <Menu.Target>
                <ActionIcon
                    variant="light"
                    size="lg"
                    disabled={disabled}
                    loading={isRecalculatePending}
                    title={t("songs:tools.title")}
                    data-testid="edit-song-tools"
                >
                    <IconTool/>
                </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
                <Menu.Item
                    leftSection={<IconHash size={16}/>}
                    onClick={() => recalculate(songId)}
                    data-testid="song-tool-recalculate-checksum"
                >
                    {t("songs:tools.recalculateChecksum.label")}
                </Menu.Item>
            </Menu.Dropdown>
        </Menu>
    );
}
