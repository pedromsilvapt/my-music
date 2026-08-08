import {Button, Group, Modal, Stack, Text, Textarea} from "@mantine/core";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {ZINDEX_MODAL} from "../../consts.ts";

interface GrantWaiverModalProps {
    opened: boolean;
    onClose: () => void;
    onConfirm: (reason: string | null) => void;
    count: number;
    loading: boolean;
}

export default function GrantWaiverModal({opened, onClose, onConfirm, count, loading}: GrantWaiverModalProps) {
    const {t} = useTranslation(["audits", "common"]);
    const [reason, setReason] = useState("");

    const handleConfirm = () => {
        onConfirm(reason.trim() || null);
        setReason("");
    };

    const handleClose = () => {
        setReason("");
        onClose();
    };

    return (
        <Modal opened={opened} onClose={handleClose} title={t("audits:grantWaiver.title")} centered zIndex={ZINDEX_MODAL}>
            <Stack>
                <Text>
                    {t("audits:grantWaiver.waivePrompt", {count})}
                </Text>
                <Textarea
                    label={t("audits:grantWaiver.reasonLabel")}
                    placeholder={t("audits:grantWaiver.reasonPlaceholder")}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    rows={3}
                    autoFocus
                />
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={handleClose}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button onClick={handleConfirm} loading={loading}>
                        {t("audits:grantWaiver.confirm")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
