import {Button, Group, Modal, Stack, TextInput} from "@mantine/core";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useCreatePlaylist} from "../../client/playlists.ts";
import {ZINDEX_MODAL} from "../../consts.ts";

interface CreatePlaylistModalProps {
    opened: boolean;
    onClose: () => void;
    onSuccess?: () => void;
}

export default function CreatePlaylistModal({opened, onClose, onSuccess}: CreatePlaylistModalProps) {
    const {t} = useTranslation(["playlists", "common"]);
    const [name, setName] = useState("");

    const createPlaylist = useCreatePlaylist({
        mutation: {
            onSuccess: () => {
                setName("");
                onClose();
                onSuccess?.();
            }
        }
    });

    const handleCreate = () => {
        if (name.trim()) {
            createPlaylist.mutate({data: {name: name.trim()}});
        }
    };

    return (
        <Modal opened={opened} onClose={onClose} title={t("playlists:createModal.title")} centered zIndex={ZINDEX_MODAL}>
            <Stack>
                <TextInput
                    label={t("playlists:createModal.nameLabel")}
                    placeholder={t("playlists:createModal.namePlaceholder")}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            handleCreate();
                        }
                    }}
                    autoFocus
                />
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={onClose}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button onClick={handleCreate} loading={createPlaylist.isPending}>
                        {t("playlists:createModal.create")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
