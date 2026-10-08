import {Alert, Button, Group, Modal, Stack, Text} from "@mantine/core";
import {IconAlertTriangle, IconShoppingBag} from "@tabler/icons-react";
import {useCallback, useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import {ZINDEX_DRAWER} from "../../consts.ts";
import {usePurchaseSongFileWithNotifications} from "../../hooks/use-purchase-song-file.ts";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import SourceSongSearchModal from "./source-song-search-modal.tsx";
import type {MetadataSearchItem} from "./use-metadata-search-schema";

interface SongPurchaseModalProps {
    opened: boolean;
    onClose: () => void;
    song: GetSongResponseSong;
}

/** A result waiting for the user to confirm its purchase, and how to tell the search what they decided. */
interface PendingPurchase {
    item: MetadataSearchItem;
    resolve: (purchased: boolean) => void;
}

/**
 * Lets the user replace the audio of a song with the one of a song purchased from a source. Nothing is purchased
 * until the user confirms the result they picked.
 */
export default function SongPurchaseModal({opened, onClose, song}: SongPurchaseModalProps) {
    const {t} = useTranslation(["songs", "common"]);
    const {purchase, isPending} = usePurchaseSongFileWithNotifications();
    const [pending, setPending] = useState<PendingPurchase | null>(null);

    const action = useMemo(() => ({
        icon: <IconShoppingBag size={16}/>,
        label: t("songs:tools.purchaseSong.purchase"),
    }), [t]);

    // The search stays open, showing as busy, until the confirmation is answered
    const handlePick = useCallback((item: MetadataSearchItem) =>
        new Promise<boolean>(resolve => setPending({item, resolve})), []);

    const handleCancel = () => {
        pending?.resolve(false);
        setPending(null);
    };

    const handleConfirm = async () => {
        if (pending == null) return;

        const purchased = await purchase(song.id, pending.item.sourceId, pending.item.song.id);

        pending.resolve(purchased);
        setPending(null);
    };

    const handleClose = () => {
        handleCancel();
        onClose();
    };

    return (
        <>
            <SourceSongSearchModal
                opened={opened}
                onClose={handleClose}
                song={song}
                title={t("songs:tools.purchaseSong.title")}
                searchPlaceholder={t("songs:tools.purchaseSong.searchPlaceholder")}
                testId="song-purchase-search"
                action={action}
                onPick={handlePick}
                header={
                    <Alert color="yellow" icon={<IconAlertTriangle size={18}/>}>
                        {t("songs:tools.purchaseSong.warning")}
                    </Alert>
                }
            />
            <Modal
                opened={pending != null}
                onClose={handleCancel}
                title={t("songs:tools.purchaseSong.confirmTitle")}
                centered
                zIndex={ZINDEX_DRAWER}
                closeOnClickOutside={!isPending}
                closeOnEscape={!isPending}
                withCloseButton={!isPending}
            >
                <Stack gap="md" data-testid="song-purchase-confirm">
                    <Text>
                        {t("songs:tools.purchaseSong.confirmMessage", {
                            title: pending?.item.song.title,
                            artists: pending?.item.song.artists.map(a => a.name).join(", "),
                            source: pending?.item.sourceName,
                            song: song.title,
                        })}
                    </Text>
                    <Group justify="flex-end" gap="xs">
                        <Button variant="subtle" onClick={handleCancel} disabled={isPending}>
                            {t("common:actions.cancel")}
                        </Button>
                        <Button
                            onClick={handleConfirm}
                            loading={isPending}
                            data-testid="song-purchase-confirm-button"
                        >
                            {t("songs:tools.purchaseSong.purchase")}
                        </Button>
                    </Group>
                </Stack>
            </Modal>
        </>
    );
}
