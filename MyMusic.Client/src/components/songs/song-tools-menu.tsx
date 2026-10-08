import {ActionIcon, Menu} from "@mantine/core";
import {IconClock, IconHash, IconShoppingBag, IconTool, IconUpload} from "@tabler/icons-react";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useRecalculateSongChecksumWithNotifications} from "../../hooks/use-recalculate-song-checksum.ts";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import SongFileUploadModal from "./song-file-upload-modal.tsx";
import SongPurchaseModal from "./song-purchase-modal.tsx";
import SongTimestampsModal from "./song-timestamps-modal.tsx";

interface SongToolsMenuProps {
    song: GetSongResponseSong;
    disabled?: boolean;
}

/**
 * Toolbox of maintenance commands to run on a song. Each tool reports its outcome in a notification: some run
 * in the background right away, others ask for input in a dialog first.
 */
export default function SongToolsMenu({song, disabled}: SongToolsMenuProps) {
    const songId = song.id;
    const {t} = useTranslation(["songs", "common"]);
    const {recalculate, isPending: isRecalculatePending} = useRecalculateSongChecksumWithNotifications();
    const [timestampsOpened, setTimestampsOpened] = useState(false);
    const [uploadOpened, setUploadOpened] = useState(false);
    const [purchaseOpened, setPurchaseOpened] = useState(false);

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
                    <Menu.Item
                        leftSection={<IconUpload size={16}/>}
                        onClick={() => setUploadOpened(true)}
                        data-testid="song-tool-upload-song"
                    >
                        {t("songs:tools.uploadSong.label")}
                    </Menu.Item>
                    <Menu.Item
                        leftSection={<IconShoppingBag size={16}/>}
                        onClick={() => setPurchaseOpened(true)}
                        data-testid="song-tool-purchase-song"
                    >
                        {t("songs:tools.purchaseSong.label")}
                    </Menu.Item>
                </Menu.Dropdown>
            </Menu>
            <SongTimestampsModal
                opened={timestampsOpened}
                onClose={() => setTimestampsOpened(false)}
                songId={songId}
            />
            <SongFileUploadModal
                opened={uploadOpened}
                onClose={() => setUploadOpened(false)}
                songId={songId}
            />
            <SongPurchaseModal
                opened={purchaseOpened}
                onClose={() => setPurchaseOpened(false)}
                song={song}
            />
        </>
    );
}
