import {ActionIcon, Menu} from "@mantine/core";
import {IconClock, IconHash, IconTool} from "@tabler/icons-react";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useRecalculateSongChecksumWithNotifications} from "../../hooks/use-recalculate-song-checksum.ts";
import SongTimestampsModal from "./song-timestamps-modal.tsx";

interface SongToolsMenuProps {
    songId: number;
    disabled?: boolean;
}

/**
 * Toolbox of maintenance commands to run on a song. Each tool reports its outcome in a notification: some run
 * in the background right away, others ask for input in a dialog first.
 */
export default function SongToolsMenu({songId, disabled}: SongToolsMenuProps) {
    const {t} = useTranslation(["songs", "common"]);
    const {recalculate, isPending: isRecalculatePending} = useRecalculateSongChecksumWithNotifications();
    const [timestampsOpened, setTimestampsOpened] = useState(false);

    return (
        <>
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
                    <Menu.Item
                        leftSection={<IconClock size={16}/>}
                        onClick={() => setTimestampsOpened(true)}
                        data-testid="song-tool-change-timestamps"
                    >
                        {t("songs:tools.changeTimestamps.label")}
                    </Menu.Item>
                </Menu.Dropdown>
            </Menu>
            <SongTimestampsModal
                opened={timestampsOpened}
                onClose={() => setTimestampsOpened(false)}
                songId={songId}
            />
        </>
    );
}
