import {
    Button,
    Checkbox,
    Group,
    Input,
    Modal,
    NumberInput,
    Rating,
    ScrollArea,
    Stack,
    Text,
    Textarea,
    TextInput,
} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {useCallback, useEffect, useState} from "react";
import {useBatchUpdateSongs, useUpdateSong,} from "../../client/songs.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import type {GetSongResponseSong} from "../../model";
import AutocompleteField, {type AutocompleteItem} from "./autocomplete-field.tsx";
import CoverUploadField from "./cover-upload-field.tsx";
import TagsAutocompleteField, {type TagsAutocompleteItem} from "./tags-autocomplete-field.tsx";

interface SongEditorModalProps {
    opened: boolean;
    onClose: () => void;
    songs: GetSongResponseSong[];
    onSuccess?: () => void;
}

interface FormState {
    title: string;
    year: number | undefined;
    lyrics: string;
    rating: number | undefined;
    explicit: boolean;
    cover: string | null;
    coverDimensions: { width: number; height: number } | null;
    album: AutocompleteItem | null;
    albumArtist: AutocompleteItem | null;
    artists: TagsAutocompleteItem[];
    genres: TagsAutocompleteItem[];
}

interface FieldCheckboxes {
    title: boolean;
    year: boolean;
    lyrics: boolean;
    rating: boolean;
    explicit: boolean;
    cover: boolean;
    album: boolean;
    albumArtist: boolean;
    artists: boolean;
    genres: boolean;
}

const initialFormState: FormState = {
    title: "",
    year: undefined,
    lyrics: "",
    rating: undefined,
    explicit: false,
    cover: null,
    coverDimensions: null,
    album: null,
    albumArtist: null,
    artists: [],
    genres: [],
};

const initialCheckboxes: FieldCheckboxes = {
    title: true,
    year: true,
    lyrics: true,
    rating: true,
    explicit: true,
    cover: true,
    album: true,
    albumArtist: true,
    artists: true,
    genres: true,
};

export default function SongEditorModal({opened, onClose, songs, onSuccess}: SongEditorModalProps) {
    const [form, setForm] = useState<FormState>(initialFormState);
    const [originalForm, setOriginalForm] = useState<FormState>(initialFormState);
    const [checkboxes, setCheckboxes] = useState<FieldCheckboxes>(initialCheckboxes);
    const queryClient = useQueryClient();

    const isBatchMode = songs.length > 1;
    const firstSong = songs[0];

    const updateSong = useUpdateSong({
        mutation: {
            onSuccess: () => {
                queryClient.invalidateQueries({queryKey: ["api", "songs"]});
                notifications.show({title: "Success", message: "Song updated successfully", color: "green"});
                onClose();
                onSuccess?.();
            },
            onError: (error) => {
                notifications.show({title: "Error", message: `Failed to update song: ${error}`, color: "red"});
            },
        },
    });

    const batchUpdateSongs = useBatchUpdateSongs({
        mutation: {
            onSuccess: (response) => {
                queryClient.invalidateQueries({queryKey: ["api", "songs"]});
                const failed = response.data.songs.filter(s => !s.success);
                if (failed.length === 0) {
                    notifications.show({title: "Success", message: "All songs updated successfully", color: "green"});
                } else {
                    notifications.show({
                        title: "Partial Success",
                        message: `${failed.length} of ${songs.length} songs failed to update`,
                        color: "yellow",
                    });
                }
                onClose();
                onSuccess?.();
            },
            onError: (error) => {
                notifications.show({title: "Error", message: `Failed to update songs: ${error}`, color: "red"});
            },
        },
    });

    useEffect(() => {
        if (opened && firstSong) {
            const songData: FormState = {
                title: firstSong.title,
                year: firstSong.year ?? undefined,
                lyrics: firstSong.lyrics ?? "",
                rating: firstSong.rating ?? undefined,
                explicit: firstSong.isExplicit,
                cover: null,
                coverDimensions: firstSong.coverDetails
                    ? {width: firstSong.coverDetails.width, height: firstSong.coverDetails.height}
                    : null,
                album: firstSong.album ? {id: firstSong.album.id, name: firstSong.album.name} : null,
                albumArtist: firstSong.album?.artist
                    ? {id: firstSong.album.artist.id, name: firstSong.album.artist.name}
                    : null,
                artists: firstSong.artists.map(a => ({id: a.id, name: a.name})),
                genres: firstSong.genres.map(g => ({id: g.id, name: g.name})),
            };
            setForm(songData);
            setOriginalForm(songData);
            setCheckboxes(initialCheckboxes);
        }
    }, [opened, firstSong]);

    const searchAlbums = useCallback(async (query: string): Promise<AutocompleteItem[]> => {
        const response = await fetch(`/api/songs/autocomplete/albums?search=${encodeURIComponent(query)}`);
        const data = await response.json();
        return data.albums.map((a: { id: number; name: string; artistName?: string }) => ({
            id: a.id,
            name: a.name,
            subtitle: a.artistName,
        }));
    }, []);

    const searchArtists = useCallback(async (query: string): Promise<TagsAutocompleteItem[]> => {
        const response = await fetch(`/api/songs/autocomplete/artists?search=${encodeURIComponent(query)}`);
        const data = await response.json();
        return data.artists.map((a: { id: number; name: string }) => ({id: a.id, name: a.name}));
    }, []);

    const searchGenres = useCallback(async (query: string): Promise<TagsAutocompleteItem[]> => {
        const response = await fetch(`/api/songs/autocomplete/genres?search=${encodeURIComponent(query)}`);
        const data = await response.json();
        return data.genres.map((g: { id: number; name: string }) => ({id: g.id, name: g.name}));
    }, []);

    const handleSave = async () => {
        if (isBatchMode) {
            const patch: Record<string, unknown> = {};

            if (checkboxes.title && form.title) patch.title = form.title;
            if (checkboxes.year && form.year !== undefined) patch.year = form.year;
            if (checkboxes.lyrics && form.lyrics) patch.lyrics = form.lyrics;
            if (checkboxes.rating && form.rating !== undefined) patch.rating = form.rating;
            if (checkboxes.explicit) patch.explicit = form.explicit;
            if (checkboxes.cover && form.cover) patch.cover = form.cover;
            if (checkboxes.album && form.album) {
                if (form.album.id > 0) {
                    patch.albumId = form.album.id;
                } else {
                    patch.albumName = form.album.name;
                }
            }
            if (checkboxes.albumArtist && form.albumArtist) {
                if (form.albumArtist.id > 0) {
                    patch.albumArtistId = form.albumArtist.id;
                } else {
                    patch.albumArtistName = form.albumArtist.name;
                }
            }
            if (checkboxes.artists && form.artists.length > 0) {
                patch.artistIds = form.artists.filter(a => a.id > 0).map(a => a.id);
                patch.artistNames = form.artists.filter(a => a.id < 0).map(a => a.name);
            }
            if (checkboxes.genres && form.genres.length > 0) {
                patch.genreIds = form.genres.filter(g => g.id > 0).map(g => g.id);
                patch.genreNames = form.genres.filter(g => g.id < 0).map(g => g.name);
            }

            batchUpdateSongs.mutate({
                data: {
                    songIds: songs.map(s => s.id),
                    patch,
                },
            });
        } else {
            const song = songs[0];
            if (!song) return;

            updateSong.mutate({
                id: song.id,
                data: {
                    songId: song.id,
                    title: form.title || undefined,
                    year: form.year,
                    lyrics: form.lyrics || undefined,
                    rating: form.rating,
                    explicit: form.explicit,
                    cover: form.cover || undefined,
                    albumId: form.album && form.album.id > 0 ? form.album.id : undefined,
                    albumName: form.album && form.album.id < 0 ? form.album.name : undefined,
                    albumArtistId: form.albumArtist && form.albumArtist.id > 0 ? form.albumArtist.id : undefined,
                    albumArtistName: form.albumArtist && form.albumArtist.id < 0 ? form.albumArtist.name : undefined,
                    artistIds: form.artists.filter(a => a.id > 0).map(a => a.id),
                    artistNames: form.artists.filter(a => a.id < 0).map(a => a.name),
                    genreIds: form.genres.filter(g => g.id > 0).map(g => g.id),
                    genreNames: form.genres.filter(g => g.id < 0).map(g => g.name),
                },
            });
        }
    };

    const isLoading = updateSong.isPending || batchUpdateSongs.isPending;

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title={isBatchMode ? `Edit ${songs.length} Songs` : "Edit Song"}
            size="lg"
            centered
            zIndex={ZINDEX_MODAL}
        >
            <ScrollArea h={500}>
                <Stack gap="md">
                    {isBatchMode && (
                        <Text size="sm" c="dimmed">
                            Editing {songs.length} songs. Only checked fields will be updated.
                        </Text>
                    )}

                    <Group gap="xs" align="center">
                        {isBatchMode && (
                            <Checkbox
                                checked={checkboxes.title}
                                onChange={(e) => {
                                    const checked = e.currentTarget.checked;
                                    setCheckboxes(prev => ({...prev, title: checked}));
                                }}
                            />
                        )}
                        <TextInput
                            label="Title"
                            placeholder="Song title"
                            value={form.title}
                            onChange={(e) => setForm(prev => ({...prev, title: e.target.value}))}
                            style={{flex: 1}}
                            disabled={isBatchMode && !checkboxes.title}
                            error={form.title !== originalForm.title && (
                                <Text size="xs" c="dimmed">Original: {originalForm.title}</Text>
                            )}
                        />
                    </Group>

                    <AutocompleteField
                        label="Album"
                        placeholder="Select or type album name"
                        value={form.album}
                        onChange={(value) => setForm(prev => ({...prev, album: value as AutocompleteItem | null}))}
                        onSearch={searchAlbums}
                        diffMode={isBatchMode}
                        originalValue={originalForm.album}
                        isChecked={checkboxes.album}
                        onCheckChange={(checked) => setCheckboxes(prev => ({...prev, album: checked}))}
                    />

                    <AutocompleteField
                        label="Album Artist"
                        placeholder="Select or type artist name"
                        value={form.albumArtist}
                        onChange={(value) => setForm(prev => ({
                            ...prev,
                            albumArtist: value as AutocompleteItem | null
                        }))}
                        onSearch={searchArtists}
                        diffMode={isBatchMode}
                        originalValue={originalForm.albumArtist}
                        isChecked={checkboxes.albumArtist}
                        onCheckChange={(checked) => setCheckboxes(prev => ({...prev, albumArtist: checked}))}
                    />

                    <TagsAutocompleteField
                        label="Artists"
                        placeholder="Select or type artist names"
                        value={form.artists}
                        onChange={(value) => setForm(prev => ({...prev, artists: value}))}
                        onSearch={searchArtists}
                        diffMode={isBatchMode}
                        originalValue={originalForm.artists}
                        isChecked={checkboxes.artists}
                        onCheckChange={(checked) => setCheckboxes(prev => ({...prev, artists: checked}))}
                    />

                    <TagsAutocompleteField
                        label="Genres"
                        placeholder="Select or type genre names"
                        value={form.genres}
                        onChange={(value) => setForm(prev => ({...prev, genres: value}))}
                        onSearch={searchGenres}
                        diffMode={isBatchMode}
                        originalValue={originalForm.genres}
                        isChecked={checkboxes.genres}
                        onCheckChange={(checked) => setCheckboxes(prev => ({...prev, genres: checked}))}
                    />

                    <Group gap="xs" align="center">
                        {isBatchMode && (
                            <Checkbox
                                checked={checkboxes.year}
                                onChange={(e) => {
                                    const checked = e.currentTarget.checked;
                                    setCheckboxes(prev => ({...prev, year: checked}));
                                }}
                            />
                        )}
                        <NumberInput
                            label="Year"
                            placeholder="Release year"
                            value={form.year}
                            onChange={(value) => setForm(prev => ({...prev, year: value as number | undefined}))}
                            style={{flex: 1}}
                            disabled={isBatchMode && !checkboxes.year}
                            min={1900}
                            max={new Date().getFullYear() + 1}
                        />
                    </Group>

                    <Group gap="xs" align="center">
                        {isBatchMode && (
                            <Checkbox
                                checked={checkboxes.rating}
                                onChange={(e) => {
                                    const checked = e.currentTarget.checked;
                                    setCheckboxes(prev => ({...prev, rating: checked}));
                                }}
                            />
                        )}
                        <Input.Wrapper label="Rating" style={{flex: 1}}>
                            <Rating
                                value={form.rating ?? 0}
                                onChange={(value) => setForm(prev => ({...prev, rating: value}))}
                                fractions={2}
                                readOnly={isBatchMode && !checkboxes.rating}
                            />
                        </Input.Wrapper>
                    </Group>

                    <Group gap="xs" align="center">
                        {isBatchMode && (
                            <Checkbox
                                checked={checkboxes.explicit}
                                onChange={(e) => {
                                    const checked = e.currentTarget.checked;
                                    setCheckboxes(prev => ({...prev, explicit: checked}));
                                }}
                            />
                        )}
                        <Checkbox
                            label="Explicit"
                            checked={form.explicit}
                            onChange={(e) => {
                                const checked = e.currentTarget.checked;
                                setForm(prev => ({...prev, explicit: checked}));
                            }}
                            disabled={isBatchMode && !checkboxes.explicit}
                        />
                    </Group>

                    <Group gap="xs" align="flex-start">
                        {isBatchMode && (
                            <Checkbox
                                checked={checkboxes.lyrics}
                                onChange={(e) => {
                                    const checked = e.currentTarget.checked;
                                    setCheckboxes(prev => ({...prev, lyrics: checked}));
                                }}
                                mt={24}
                            />
                        )}
                        <Textarea
                            label="Lyrics"
                            placeholder="Song lyrics"
                            value={form.lyrics}
                            onChange={(e) => setForm(prev => ({...prev, lyrics: e.target.value}))}
                            style={{flex: 1}}
                            rows={5}
                            disabled={isBatchMode && !checkboxes.lyrics}
                        />
                    </Group>

                    <CoverUploadField
                        value={form.cover}
                        onChange={(value, dimensions) => setForm(prev => ({
                            ...prev,
                            cover: value,
                            coverDimensions: dimensions
                        }))}
                        currentCoverId={firstSong?.cover}
                        currentDimensions={form.coverDimensions}
                        diffMode={isBatchMode}
                        isChecked={checkboxes.cover}
                        onCheckChange={(checked) => setCheckboxes(prev => ({...prev, cover: checked}))}
                    />
                </Stack>
            </ScrollArea>

            <Group justify="flex-end" mt="md">
                <Button variant="subtle" onClick={onClose} disabled={isLoading}>
                    Cancel
                </Button>
                <Button onClick={handleSave} loading={isLoading}>
                    Save Changes
                </Button>
            </Group>
        </Modal>
    );
}
