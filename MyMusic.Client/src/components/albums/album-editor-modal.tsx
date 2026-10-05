import {Alert, Button, Group, NumberInput, Stack, TextInput} from "@mantine/core";
import {type ContextModalProps, modals} from "@mantine/modals";
import {type FormEvent, useEffect, useState} from "react";
import {useTranslation} from "react-i18next";
import {ALBUM_PLACEHOLDER_NAME} from "../../consts.ts";
import {useUpdateAlbumsWithInvalidation} from "../../hooks/use-update-albums.ts";
import {ApiProblemError} from "../../utils/api-problem.ts";
import EditorPager from "../common/editor-pager.tsx";

export interface EditableAlbum {
    id: number;
    name: string;
    year?: number | null;
}

export interface AlbumEditorModalInnerProps {
    albums: EditableAlbum[];
    onSuccess?: () => void;
}

interface AlbumForm {
    name: string;
    year: number | null;
}

const isModified = (album: EditableAlbum, form: AlbumForm) =>
    form.name.trim() !== album.name || form.year !== (album.year ?? null);

/**
 * Edits the name and year of one or more albums, stepping through them one at a time. The albums that
 * changed are saved together, as a single operation: the dialog stays open until the server is done
 * rewriting the songs of the renamed ones, and shows why when it refuses.
 */
export default function AlbumEditorModal({context, id, innerProps}: ContextModalProps<AlbumEditorModalInnerProps>) {
    const {t} = useTranslation(["albums", "common"]);
    const {albums, onSuccess} = innerProps;
    const [forms, setForms] = useState<AlbumForm[]>(() =>
        albums.map(album => ({name: album.name, year: album.year ?? null})));
    const [index, setIndex] = useState(0);
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const updateAlbums = useUpdateAlbumsWithInvalidation();

    // The dialog cannot be dismissed while the albums are being saved
    useEffect(() => {
        modals.updateContextModal({
            modalId: id,
            closeOnEscape: !saving,
            closeOnClickOutside: !saving,
            withCloseButton: !saving,
        });
    }, [id, saving]);

    const album = albums[index]!;
    const form = forms[index]!;
    const isPlaceholder = album.name === ALBUM_PLACEHOLDER_NAME;

    const modified = albums
        .map((album, i) => ({album, form: forms[i]!}))
        .filter(({album, form}) => isModified(album, form));
    const canSave = modified.length > 0 && modified.every(({form}) => form.name.trim() !== "");

    const setForm = (changes: Partial<AlbumForm>) =>
        setForms(forms => forms.map((form, i) => i === index ? {...form, ...changes} : form));

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();

        if (!canSave || saving) {
            return;
        }

        setError(null);
        setSaving(true);
        try {
            await updateAlbums(modified.map(({album, form}) => ({
                id: album.id,
                name: form.name.trim(),
                year: form.year,
            })));
        } catch (error) {
            setSaving(false);
            // The server explains why it refused (e.g. the artist already has an album with that name)
            setError((error instanceof ApiProblemError ? error.detail : undefined)
                ?? t("albums:editModal.saveFailed", {count: modified.length}));
            return;
        }

        context.closeModal(id);
        onSuccess?.();
    };

    return (
        <form onSubmit={handleSubmit} data-testid="album-editor">
            <Stack>
                {albums.length > 1 && (
                    <EditorPager
                        index={index}
                        count={albums.length}
                        label={album.name}
                        modified={isModified(album, form)}
                        onChange={setIndex}
                        previousLabel={t("albums:editModal.previous")}
                        nextLabel={t("albums:editModal.next")}
                        testId="album-editor-pager"
                    />
                )}
                <TextInput
                    label={t("albums:editModal.nameLabel")}
                    placeholder={t("albums:editModal.namePlaceholder")}
                    description={isPlaceholder ? t("albums:editModal.placeholderHint") : undefined}
                    value={form.name}
                    onChange={(e) => setForm({name: e.target.value})}
                    disabled={isPlaceholder || saving}
                    maxLength={256}
                    data-autofocus
                    data-testid="album-editor-name"
                />
                <NumberInput
                    label={t("albums:editModal.yearLabel")}
                    value={form.year ?? ""}
                    onChange={value => setForm({year: typeof value === "number" ? value : null})}
                    disabled={saving}
                    min={1}
                    max={9999}
                    allowDecimal={false}
                    allowNegative={false}
                    data-testid="album-editor-year"
                />
                {error && (
                    <Alert color="red" data-testid="album-editor-error">
                        {error}
                    </Alert>
                )}
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={() => context.closeModal(id)} disabled={saving}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button type="submit" loading={saving} disabled={!canSave} data-testid="album-editor-submit">
                        {t("common:actions.save")}
                    </Button>
                </Group>
            </Stack>
        </form>
    );
}
