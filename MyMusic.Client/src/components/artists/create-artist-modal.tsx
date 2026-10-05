import {Button, Group, Modal, Stack, TextInput} from "@mantine/core";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useCreateArtist} from "../../client/artists.ts";
import {ZINDEX_MODAL} from "../../consts.ts";

interface CreateArtistModalProps {
    opened: boolean;
    onClose: () => void;
}

export default function CreateArtistModal({opened, onClose}: CreateArtistModalProps) {
    const {t} = useTranslation(["artists", "common"]);
    const [name, setName] = useState("");
    const [error, setError] = useState<string | null>(null);

    const handleClose = () => {
        setName("");
        setError(null);
        onClose();
    };

    const createArtist = useCreateArtist({
        mutation: {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    setError(responseData?.detail || t("artists:createModal.createFailed"));
                    return;
                }

                handleClose();
            },
            onError: () => setError(t("artists:createModal.createFailed")),
        }
    });

    const handleCreate = () => {
        if (name.trim()) {
            setError(null);
            createArtist.mutate({data: {name: name.trim()}});
        }
    };

    return (
        <Modal opened={opened} onClose={handleClose} title={t("artists:createModal.title")} centered
               zIndex={ZINDEX_MODAL}>
            <Stack>
                <TextInput
                    label={t("artists:createModal.nameLabel")}
                    placeholder={t("artists:createModal.namePlaceholder")}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            handleCreate();
                        }
                    }}
                    error={error}
                    errorProps={{'data-testid': 'create-artist-error'}}
                    maxLength={256}
                    data-autofocus
                    data-testid="create-artist-name"
                />
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={handleClose}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button onClick={handleCreate} loading={createArtist.isPending} data-testid="create-artist-submit" disabled={!name.trim()}>
                        {t("artists:createModal.create")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
