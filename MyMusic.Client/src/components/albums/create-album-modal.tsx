import {Button, Group, Modal, NumberInput, Stack, TextInput} from "@mantine/core";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useCreateAlbumWithArtistsInvalidation} from "../../hooks/use-create-album.ts";
import type {ListArtistItem} from "../../model";
import ArtistSelect from "../artists/artist-select.tsx";

interface CreateAlbumModalProps {
    opened: boolean;
    onClose: () => void;
    /** The artist the picker starts with; the user can still pick another one. */
    initialArtist?: ListArtistItem | null;
}

export default function CreateAlbumModal({opened, onClose, initialArtist}: CreateAlbumModalProps) {
    const {t} = useTranslation(["albums", "common"]);
    const [name, setName] = useState("");
    const [pickedArtist, setPickedArtist] = useState<ListArtistItem | null>(null);
    const [year, setYear] = useState<number | null>(null);
    const [error, setError] = useState<string | null>(null);

    const artist = pickedArtist ?? initialArtist ?? null;

    const handleClose = () => {
        setName("");
        setPickedArtist(null);
        setYear(null);
        setError(null);
        onClose();
    };

    const createAlbum = useCreateAlbumWithArtistsInvalidation({
        mutation: {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    setError(responseData?.detail || t("albums:createModal.createFailed"));
                    return;
                }

                handleClose();
            },
            onError: () => setError(t("albums:createModal.createFailed")),
        }
    });

    const canCreate = name.trim() !== "" && artist !== null;

    const handleCreate = () => {
        if (canCreate) {
            setError(null);
            createAlbum.mutate({data: {name: name.trim(), artistId: artist.id, year}});
        }
    };

    return (
        <Modal opened={opened} onClose={handleClose} title={t("albums:createModal.title")} centered
               zIndex={ZINDEX_MODAL}>
            <Stack>
                <TextInput
                    label={t("albums:createModal.nameLabel")}
                    placeholder={t("albums:createModal.namePlaceholder")}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                            handleCreate();
                        }
                    }}
                    error={error}
                    errorProps={{'data-testid': 'create-album-error'}}
                    maxLength={256}
                    data-autofocus
                    data-testid="create-album-name"
                />
                <ArtistSelect
                    label={t("albums:createModal.artistLabel")}
                    value={artist}
                    onChange={setPickedArtist}
                    enabled={opened}
                    testId="create-album-artist"
                />
                <NumberInput
                    label={t("albums:createModal.yearLabel")}
                    value={year ?? ""}
                    onChange={value => setYear(typeof value === "number" ? value : null)}
                    min={1}
                    max={9999}
                    allowDecimal={false}
                    allowNegative={false}
                    data-testid="create-album-year"
                />
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={handleClose}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button onClick={handleCreate} loading={createAlbum.isPending} data-testid="create-album-submit" disabled={!canCreate}>
                        {t("albums:createModal.create")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}
