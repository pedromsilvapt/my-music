import {Alert, Button, Group, Stack, TextInput} from "@mantine/core";
import {type ContextModalProps, modals} from "@mantine/modals";
import {type FormEvent, useEffect, useState} from "react";
import {useTranslation} from "react-i18next";
import {ARTIST_PLACEHOLDER_NAME} from "../../consts.ts";
import {useUpdateArtistsWithInvalidation} from "../../hooks/use-update-artists.ts";
import {ApiProblemError} from "../../utils/api-problem.ts";
import EditorPager from "../common/editor-pager.tsx";

export interface EditableArtist {
    id: number;
    name: string;
}

export interface ArtistEditorModalInnerProps {
    artists: EditableArtist[];
    onSuccess?: () => void;
}

const isModified = (artist: EditableArtist, name: string) => name.trim() !== artist.name;

/**
 * Renames one or more artists, stepping through them one at a time. The artists that changed are saved
 * together, as a single operation: the dialog stays open until the server is done rewriting their songs,
 * and shows why when it refuses.
 */
export default function ArtistEditorModal({context, id, innerProps}: ContextModalProps<ArtistEditorModalInnerProps>) {
    const {t} = useTranslation(["artists", "common"]);
    const {artists, onSuccess} = innerProps;
    const [names, setNames] = useState<string[]>(() => artists.map(artist => artist.name));
    const [index, setIndex] = useState(0);
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const updateArtists = useUpdateArtistsWithInvalidation();

    // The dialog cannot be dismissed while the artists are being saved
    useEffect(() => {
        modals.updateContextModal({
            modalId: id,
            closeOnEscape: !saving,
            closeOnClickOutside: !saving,
            withCloseButton: !saving,
        });
    }, [id, saving]);

    const artist = artists[index]!;
    const name = names[index]!;
    const isPlaceholder = artist.name === ARTIST_PLACEHOLDER_NAME;

    const modified = artists
        .map((artist, i) => ({artist, name: names[i]!}))
        .filter(({artist, name}) => isModified(artist, name));
    const canSave = modified.length > 0 && modified.every(({name}) => name.trim() !== "");

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();

        if (!canSave || saving) {
            return;
        }

        setError(null);
        setSaving(true);
        try {
            await updateArtists(modified.map(({artist, name}) => ({id: artist.id, name: name.trim()})));
        } catch (error) {
            setSaving(false);
            // The server explains why it refused
            setError((error instanceof ApiProblemError ? error.detail : undefined)
                ?? t("artists:editModal.saveFailed", {count: modified.length}));
            return;
        }

        context.closeModal(id);
        onSuccess?.();
    };

    return (
        <form onSubmit={handleSubmit} data-testid="artist-editor">
            <Stack>
                {artists.length > 1 && (
                    <EditorPager
                        index={index}
                        count={artists.length}
                        label={artist.name}
                        modified={isModified(artist, name)}
                        onChange={setIndex}
                        previousLabel={t("artists:editModal.previous")}
                        nextLabel={t("artists:editModal.next")}
                        testId="artist-editor-pager"
                    />
                )}
                <TextInput
                    label={t("artists:editModal.nameLabel")}
                    placeholder={t("artists:editModal.namePlaceholder")}
                    description={isPlaceholder ? t("artists:editModal.placeholderHint") : undefined}
                    value={name}
                    onChange={(e) => setNames(names => names.map((name, i) => i === index ? e.target.value : name))}
                    disabled={isPlaceholder || saving}
                    maxLength={256}
                    data-autofocus
                    data-testid="artist-editor-name"
                />
                {error && (
                    <Alert color="red" data-testid="artist-editor-error">
                        {error}
                    </Alert>
                )}
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={() => context.closeModal(id)} disabled={saving}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button type="submit" loading={saving} disabled={!canSave} data-testid="artist-editor-submit">
                        {t("common:actions.save")}
                    </Button>
                </Group>
            </Stack>
        </form>
    );
}
