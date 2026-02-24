import {Button, Group, Modal, Stack, Text, Textarea} from "@mantine/core";
import {useState} from "react";
import {ZINDEX_MODAL} from "../../consts.ts";

interface GrantWaiverModalProps {
    opened: boolean;
    onClose: () => void;
    onConfirm: (reason: string | null) => void;
    count: number;
    loading: boolean;
}

export default function GrantWaiverModal({opened, onClose, onConfirm, count, loading}: GrantWaiverModalProps) {
    const [reason, setReason] = useState("");

    const handleConfirm = () => {
        onConfirm(reason.trim() || null);
        setReason("");
    };

    const handleClose = () => {
        setReason("");
        onClose();
    };

    const itemText = count === 1 ? "non-conformity" : "non-conformities";

    return (
        <Modal opened={opened} onClose={handleClose} title="Grant Waiver" centered zIndex={ZINDEX_MODAL}>
            <Stack>
                <Text>
                    Enter an optional reason for waiving {count} {itemText}:
                </Text>
                <Textarea
                    label="Waiver Reason"
                    placeholder="Optional reason for the waiver..."
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    rows={3}
                    autoFocus
                />
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={handleClose}>
                        Cancel
                    </Button>
                    <Button onClick={handleConfirm} loading={loading}>
                        Grant Waiver
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
