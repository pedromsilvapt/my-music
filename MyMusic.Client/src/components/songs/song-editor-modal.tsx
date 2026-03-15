import {
    ActionIcon,
    Box,
    Button,
    Checkbox,
    Group,
    Input,
    Modal,
    NumberInput,
    Rating,
    ScrollArea,
    Stack,
    Switch,
    Text,
    Textarea,
    TextInput,
} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useQueryClient} from "@tanstack/react-query";
import {IconChevronLeft, IconChevronRight, IconRefresh, IconSearch} from "@tabler/icons-react";
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {
    useBatchMultiUpdateSongs,
    useFetchSongMetadata,
    useUpdateSong,
} from "../../client/songs.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import type {GetSongResponseSong, SongMetadataDiff, UpdateSongRequest} from "../../model";
import type {BatchMultiUpdateSongResult} from "../../model/batchMultiUpdateSongResult";
import AutocompleteField, {type AutocompleteItem} from "./autocomplete-field.tsx";
import CoverUploadField from "./cover-upload-field.tsx";
import MetadataSearchModal from "./metadata-search-modal.tsx";
import TagsAutocompleteField, {type TagsAutocompleteItem} from "./tags-autocomplete-field.tsx";
import {
    checkboxesFromMetadata,
    createInitialCheckboxes,
    formStateFromMetadata,
    formStateFromSong,
    hasPendingChanges,
    type FormState,
    type FieldCheckboxes,
} from "./song-edit-types.ts";

interface SongEditorModalProps {
    opened: boolean;
    onClose: () => void;
    songs: GetSongResponseSong[];
    metadata?: Map<number, SongMetadataDiff>;
    onSuccess?: () => void;
}

interface SongEditState {
    song: GetSongResponseSong;
    metadata: SongMetadataDiff | null;
    form: FormState;
    checkboxes: FieldCheckboxes;
    originalForm: FormState;
}

function createSongEditState(song: GetSongResponseSong, metadata?: SongMetadataDiff | null): SongEditState {
    const originalForm = formStateFromSong(song);
    const form = metadata ? formStateFromMetadata(metadata, originalForm) : { ...originalForm };
    const checkboxes = metadata ? checkboxesFromMetadata(metadata) : createInitialCheckboxes();
    return { song, metadata: metadata ?? null, form, checkboxes, originalForm };
}

export default function SongEditorModal({
    opened,
    onClose,
    songs,
    metadata: externalMetadata,
}: SongEditorModalProps) {
    const [editStates, setEditStates] = useState<Map<number, SongEditState>>(new Map());
    const [currentIndex, setCurrentIndex] = useState(0);
    const [metadataSearchOpened, setMetadataSearchOpened] = useState(false);
    const shouldCloseAfterSave = useRef(false);
    const savedSongIds = useRef<Set<number>>(new Set());
    const queryClient = useQueryClient();

    const isMultiSong = songs.length > 1;
    const currentState = currentIndex < songs.length ? editStates.get(songs[currentIndex]?.id) : null;

    const updateSong = useUpdateSong({
        mutation: {
            onSuccess: (response, variables) => {
                if (response.status >= 400) {
                    const errorDetail = (response.data as any)?.detail || "Unknown error";
                    notifications.show({title: "Error", message: `Failed to update song: ${errorDetail}`, color: "red"});
                    return;
                }

                const savedSongId = variables.data.songId;
                savedSongIds.current.add(savedSongId);

                setEditStates(prev => {
                    const newMap = new Map(prev);
                    const state = newMap.get(savedSongId);
                    if (state) {
                        newMap.set(savedSongId, {
                            ...state,
                            metadata: null,
                            originalForm: { ...state.form },
                        });
                    }
                    return newMap;
                });

                queryClient.invalidateQueries({queryKey: ["api", "songs"]});
                queryClient.invalidateQueries({queryKey: ["api", "audits"]});
                notifications.show({title: "Success", message: "Song updated successfully", color: "green"});
                if (shouldCloseAfterSave.current) {
                    handleClose();
                } else if (currentIndex < songs.length - 1) {
                    setCurrentIndex(currentIndex + 1);
                }
                shouldCloseAfterSave.current = false;
            },
            onError: (error) => {
                notifications.show({title: "Error", message: `Failed to update song: ${error}`, color: "red"});
            },
        },
    });

    const batchMultiUpdateSongs = useBatchMultiUpdateSongs({
        mutation: {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const errorDetail = (response.data as any)?.detail || "Unknown error";
                    notifications.show({title: "Error", message: `Failed to update songs: ${errorDetail}`, color: "red"});
                    return;
                }

                const successfulSongs = response.data.songs
                    .filter((s: BatchMultiUpdateSongResult) => s.success)
                    .map((s: BatchMultiUpdateSongResult) => s.id);

                successfulSongs.forEach(id => savedSongIds.current.add(id));

                setEditStates(prev => {
                    const newMap = new Map(prev);
                    successfulSongs.forEach(id => {
                        const state = newMap.get(id);
                        if (state) {
                            newMap.set(id, {
                                ...state,
                                metadata: null,
                                originalForm: { ...state.form },
                            });
                        }
                    });
                    return newMap;
                });

                queryClient.invalidateQueries({queryKey: ["api", "songs"]});
                queryClient.invalidateQueries({queryKey: ["api", "audits"]});
                const failed = response.data.songs.filter((s: BatchMultiUpdateSongResult) => !s.success);
                if (failed.length === 0) {
                    notifications.show({title: "Success", message: "All songs updated successfully", color: "green"});
                } else {
                    notifications.show({
                        title: "Partial Success",
                        message: `${failed.length} of ${songs.length} songs failed to update`,
                        color: "yellow",
                    });
                }
                handleClose();
            },
            onError: (error) => {
                notifications.show({title: "Error", message: `Failed to update songs: ${error}`, color: "red"});
            },
        },
    });

    const fetchMetadata = useFetchSongMetadata({
        mutation: {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const errorDetail = (response.data as any)?.detail || "Unknown error";
                    notifications.show({title: "Error", message: `Failed to fetch metadata: ${errorDetail}`, color: "red"});
                    return;
                }
                if (currentState && response.data.metadata) {
                    handleMetadataFetched(response.data.metadata);
                }
            },
            onError: (error) => {
                notifications.show({title: "Error", message: `Failed to fetch metadata: ${error}`, color: "red"});
            },
        },
    });

    useEffect(() => {
        if (opened && songs.length > 0) {
            setEditStates(prev => {
                const newMap = new Map(prev);
                let hasNewSongs = false;
                songs.forEach((song) => {
                    if (!newMap.has(song.id)) {
                        const songMetadata = externalMetadata?.get(song.id) ?? null;
                        newMap.set(song.id, createSongEditState(song, songMetadata));
                        hasNewSongs = true;
                    }
                });
                if (hasNewSongs) {
                    return newMap;
                }
                return prev;
            });

            if (currentIndex >= songs.length || currentIndex < 0) {
                setCurrentIndex(0);
            }
        }
    }, [opened, songs, externalMetadata, currentIndex]);

    const handleMetadataFetched = useCallback((metadata: SongMetadataDiff) => {
        if (!currentState) return;

        const newForm = formStateFromMetadata(metadata, currentState.originalForm);
        const newCheckboxes = checkboxesFromMetadata(metadata);

        setEditStates((prev) => {
            const newMap = new Map(prev);
            newMap.set(currentState.song.id, {
                ...currentState,
                metadata,
                form: newForm,
                checkboxes: newCheckboxes,
            });
            return newMap;
        });
    }, [currentState]);

    const handleAutoFetchMetadata = useCallback(() => {
        if (!currentState) return;
        fetchMetadata.mutate({ id: currentState.song.id });
    }, [currentState, fetchMetadata]);

    const handleMetadataSelect = useCallback((metadata: SongMetadataDiff) => {
        handleMetadataFetched(metadata);
    }, [handleMetadataFetched]);

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

    const updateCurrentState = useCallback((updates: Partial<SongEditState>) => {
        if (!currentState) return;
        setEditStates((prev) => {
            const newMap = new Map(prev);
            newMap.set(currentState.song.id, { ...currentState, ...updates });
            return newMap;
        });
    }, [currentState]);

    const handleFormChange = useCallback((updates: Partial<FormState>) => {
        if (!currentState) return;
        updateCurrentState({ form: { ...currentState.form, ...updates } });
    }, [currentState, updateCurrentState]);

    const handleCheckboxChange = useCallback((field: keyof FieldCheckboxes, checked: boolean) => {
        if (!currentState) return;
        updateCurrentState({ checkboxes: { ...currentState.checkboxes, [field]: checked } });
    }, [currentState, updateCurrentState]);

    const handleSelectAll = useCallback((checked: boolean) => {
        if (!currentState?.metadata) return;
        const allFields = Object.keys(currentState.checkboxes) as (keyof FieldCheckboxes)[];
        const hasMetadata = allFields.filter((field) => {
            const diffField = field === "albumArtist" ? "album" : field;
            const metadataValue = currentState.metadata?.[diffField as keyof SongMetadataDiff];
            return metadataValue != null;
        });
        const newCheckboxes = { ...currentState.checkboxes };
        hasMetadata.forEach((field) => {
            newCheckboxes[field] = checked;
        });
        updateCurrentState({ checkboxes: newCheckboxes });
    }, [currentState, updateCurrentState]);

    const handleSaveCurrent = async (closeAfterSave = false) => {
        if (!currentState) return;

        shouldCloseAfterSave.current = closeAfterSave;

        const { form, checkboxes, metadata } = currentState;
        const song = currentState.song;

        const hasDiff = metadata != null;

        const data: UpdateSongRequest = { songId: song.id };

        if (!hasDiff || checkboxes.title) {
            data.title = form.title || null;
        }
        if (!hasDiff || checkboxes.year) {
            data.year = form.year ?? null;
        }
        if (!hasDiff || checkboxes.lyrics) {
            data.lyrics = form.lyrics || null;
        }
        if (!hasDiff || checkboxes.rating) {
            data.rating = form.rating ?? null;
        }
        if (!hasDiff || checkboxes.explicit) {
            data.explicit = form.explicit;
        }
        if (!hasDiff || checkboxes.cover) {
            data.cover = form.cover || null;
        }
        if (!hasDiff || checkboxes.album) {
            if (form.album && form.album.id > 0) {
                data.albumId = form.album.id;
            } else if (form.album) {
                data.albumName = form.album.name;
            }
        }
        if (!hasDiff || checkboxes.albumArtist) {
            if (form.albumArtist && form.albumArtist.id > 0) {
                data.albumArtistId = form.albumArtist.id;
            } else if (form.albumArtist) {
                data.albumArtistName = form.albumArtist.name;
            }
        }
        if (!hasDiff || checkboxes.artists) {
            data.artistIds = form.artists.filter(a => a.id > 0).map(a => a.id);
            data.artistNames = form.artists.filter(a => a.id < 0).map(a => a.name);
        }
        if (!hasDiff || checkboxes.genres) {
            data.genreIds = form.genres.filter(g => g.id > 0).map(g => g.id);
            data.genreNames = form.genres.filter(g => g.id < 0).map(g => g.name);
        }

        updateSong.mutate({
            id: song.id,
            data,
        });
    };

    const handleSaveAll = async () => {
        const statesWithChanges = Array.from(editStates.values())
            .filter(state => {
                if (savedSongIds.current.has(state.song.id)) return false;
                return hasPendingChanges(state);
            });

        if (statesWithChanges.length === 0) {
            notifications.show({title: "Info", message: "No changes to save", color: "blue"});
            return;
        }

        const updates = statesWithChanges.map((state) => {
            const { song, form, checkboxes } = state;
            const update: Record<string, unknown> = { songId: song.id };

            if (checkboxes.title && form.title) update.title = form.title;
            if (checkboxes.year && form.year !== undefined) update.year = form.year;
            if (checkboxes.lyrics && form.lyrics) update.lyrics = form.lyrics;
            if (checkboxes.rating && form.rating !== undefined) update.rating = form.rating;
            if (checkboxes.explicit) update.explicit = form.explicit;
            if (checkboxes.cover && form.cover) update.cover = form.cover;
            if (checkboxes.album && form.album) {
                if (form.album.id > 0) {
                    update.albumId = form.album.id;
                } else {
                    update.albumName = form.album.name;
                }
            }
            if (checkboxes.albumArtist && form.albumArtist) {
                if (form.albumArtist.id > 0) {
                    update.albumArtistId = form.albumArtist.id;
                } else {
                    update.albumArtistName = form.albumArtist.name;
                }
            }
            if (checkboxes.artists && form.artists.length > 0) {
                update.artistIds = form.artists.filter(a => a.id > 0).map(a => a.id);
                update.artistNames = form.artists.filter(a => a.id < 0).map(a => a.name);
            }
            if (checkboxes.genres && form.genres.length > 0) {
                update.genreIds = form.genres.filter(g => g.id > 0).map(g => g.id);
                update.genreNames = form.genres.filter(g => g.id < 0).map(g => g.name);
            }

            return update as {
                songId: number;
                title?: string;
                year?: number;
                lyrics?: string;
                rating?: number;
                explicit?: boolean;
                cover?: string;
                albumId?: number;
                albumName?: string;
                albumArtistId?: number;
                albumArtistName?: string;
                artistIds?: number[];
                artistNames?: string[];
                genreIds?: number[];
                genreNames?: string[];
            };
        });

        batchMultiUpdateSongs.mutate({
            data: { updates },
        });
    };

    const handleClose = () => {
        setEditStates(new Map());
        setCurrentIndex(0);
        setMetadataSearchOpened(false);
        savedSongIds.current.clear();
        onClose();
    };

    const handlePrevious = () => {
        if (currentIndex > 0) {
            setCurrentIndex(currentIndex - 1);
        }
    };

    const handleNext = () => {
        if (currentIndex < songs.length - 1) {
            setCurrentIndex(currentIndex + 1);
        }
    };

    const isLoading = updateSong.isPending || batchMultiUpdateSongs.isPending || fetchMetadata.isPending;
    const hasMetadata = currentState?.metadata != null;
    const metadataFieldCount = hasMetadata
        ? Object.values(currentState!.metadata!).filter((v) => v != null).length
        : 0;

    const getMetadataDiffFields = useCallback(() => {
        if (!currentState?.metadata) return [];
        const allFields = Object.keys(currentState.checkboxes) as (keyof FieldCheckboxes)[];
        return allFields.filter((field) => {
            const diffField = field === "albumArtist" ? "album" : field;
            return currentState.metadata?.[diffField as keyof SongMetadataDiff] != null;
        });
    }, [currentState]);

    const applyChangesState = useMemo(() => {
        const diffFields = getMetadataDiffFields();
        if (diffFields.length === 0) return { checked: false, indeterminate: false };
        const checkedCount = diffFields.filter(f => currentState?.checkboxes[f]).length;
        return {
            checked: checkedCount === diffFields.length,
            indeterminate: checkedCount > 0 && checkedCount < diffFields.length
        };
    }, [currentState, getMetadataDiffFields]);

    if (!currentState) {
        return null;
    }

    return (
        <>
            <MetadataSearchModal
                opened={metadataSearchOpened}
                onClose={() => setMetadataSearchOpened(false)}
                song={currentState.song}
                onSelect={handleMetadataSelect}
            />

            <Modal
                opened={opened}
                onClose={handleClose}
                title={isMultiSong ? `Edit Song ${currentIndex + 1} of ${songs.length}` : "Edit Song"}
                size="lg"
                centered
                zIndex={ZINDEX_MODAL}
            >
                <ScrollArea>
                    <Stack gap="md">
                        {isMultiSong && (
                            <Group justify="space-between" align="center">
                                <Group gap="xs">
                                    <ActionIcon
                                        variant="light"
                                        onClick={handlePrevious}
                                        disabled={currentIndex === 0}
                                    >
                                        <IconChevronLeft/>
                                    </ActionIcon>
                                    <Text size="sm" c="dimmed">
                                        {currentState.song.title}
                                        {currentState && !savedSongIds.current.has(currentState.song.id) && hasPendingChanges(currentState) && " *"}
                                    </Text>
                                    <ActionIcon
                                        variant="light"
                                        onClick={handleNext}
                                        disabled={currentIndex === songs.length - 1}
                                    >
                                        <IconChevronRight/>
                                    </ActionIcon>
                                </Group>
                            </Group>
                        )}

                        {hasMetadata && metadataFieldCount > 0 && (
                            <Checkbox
                                label="Apply Changes"
                                checked={applyChangesState.checked}
                                indeterminate={applyChangesState.indeterminate}
                                onChange={(e) => handleSelectAll(e.currentTarget.checked)}
                            />
                        )}

                        <Group gap="xs" align="flex-end">
                            {hasMetadata && currentState.metadata?.title && (
                                <Checkbox
                                    checked={currentState.checkboxes.title}
                                    onChange={(e) => handleCheckboxChange("title", e.currentTarget.checked)}
                                    mt={24}
                                />
                            )}
                            {hasMetadata && currentState.metadata?.title && (
                                <Input.Wrapper label="Title (old)">
                                    <Input
                                        value={currentState.metadata.title.old ?? ""}
                                        readOnly
                                        styles={{
                                            input: {
                                                borderColor: currentState.checkboxes.title
                                                    ? 'var(--mantine-color-red-6)'
                                                    : 'var(--mantine-color-gray-5)',
                                                backgroundColor: currentState.checkboxes.title
                                                    ? 'var(--mantine-color-red-0)'
                                                    : 'var(--mantine-color-gray-1)',
                                                color: 'var(--mantine-color-gray-7)',
                                            }
                                        }}
                                    />
                                </Input.Wrapper>
                            )}
                            <Box style={{flex: 1}}>
                                <TextInput
                                    label={hasMetadata && currentState.metadata?.title ? "Title (new)" : "Title"}
                                    placeholder="Song title"
                                    value={currentState.form.title}
                                    onChange={(e) => handleFormChange({title: e.target.value})}
                                    disabled={hasMetadata && !!currentState.metadata?.title && !currentState.checkboxes.title}
                                    styles={hasMetadata && currentState.metadata?.title ? {
                                        input: {
                                            borderColor: currentState.checkboxes.title
                                                ? 'var(--mantine-color-green-6)'
                                                : 'var(--mantine-color-gray-5)',
                                            backgroundColor: currentState.checkboxes.title
                                                ? 'var(--mantine-color-green-0)'
                                                : 'var(--mantine-color-gray-1)',
                                        }
                                    } : undefined}
                                />
                            </Box>
                        </Group>

                        <AutocompleteField
                            label="Album"
                            placeholder="Select or type album name"
                            value={currentState.form.album}
                            onChange={(value) => handleFormChange({album: value as AutocompleteItem | null})}
                            onSearch={searchAlbums}
                            diffMode={hasMetadata && !!currentState.metadata?.album}
                            originalValue={currentState.originalForm.album}
                            isChecked={currentState.checkboxes.album}
                            onCheckChange={(checked) => handleCheckboxChange("album", checked)}
                            originalDisplayValue={currentState.metadata?.album?.old?.name}
                        />

                        <AutocompleteField
                            label="Album Artist"
                            placeholder="Select or type artist name"
                            value={currentState.form.albumArtist}
                            onChange={(value) => handleFormChange({albumArtist: value as AutocompleteItem | null})}
                            onSearch={searchArtists}
                            diffMode={hasMetadata && !!currentState.metadata?.album?.new?.artistName}
                            originalValue={currentState.originalForm.albumArtist}
                            isChecked={currentState.checkboxes.albumArtist}
                            onCheckChange={(checked) => handleCheckboxChange("albumArtist", checked)}
                            originalDisplayValue={currentState.metadata?.album?.old?.artistName ?? undefined}
                        />

                        <TagsAutocompleteField
                            label="Artists"
                            placeholder="Select or type artist names"
                            value={currentState.form.artists}
                            onChange={(value) => handleFormChange({artists: value})}
                            onSearch={searchArtists}
                            diffMode={hasMetadata && !!currentState.metadata?.artists}
                            originalValue={currentState.originalForm.artists}
                            isChecked={currentState.checkboxes.artists}
                            onCheckChange={(checked) => handleCheckboxChange("artists", checked)}
                            originalDisplayValue={currentState.metadata?.artists?.old?.map(a => a.name).join(", ")}
                        />

                        <TagsAutocompleteField
                            label="Genres"
                            placeholder="Select or type genre names"
                            value={currentState.form.genres}
                            onChange={(value) => handleFormChange({genres: value})}
                            onSearch={searchGenres}
                            diffMode={hasMetadata && !!currentState.metadata?.genres}
                            originalValue={currentState.originalForm.genres}
                            isChecked={currentState.checkboxes.genres}
                            onCheckChange={(checked) => handleCheckboxChange("genres", checked)}
                            originalDisplayValue={currentState.metadata?.genres?.old?.join(", ")}
                        />

                        <Group gap="xs" align="flex-end">
                            {hasMetadata && currentState.metadata?.year && (
                                <Checkbox
                                    checked={currentState.checkboxes.year}
                                    onChange={(e) => handleCheckboxChange("year", e.currentTarget.checked)}
                                    mt={24}
                                />
                            )}
                            {hasMetadata && currentState.metadata?.year && (
                                <Input.Wrapper label="Year (old)">
                                    <NumberInput
                                        value={currentState.metadata.year.old}
                                        readOnly
                                        styles={{
                                            input: {
                                                borderColor: currentState.checkboxes.year
                                                    ? 'var(--mantine-color-red-6)'
                                                    : 'var(--mantine-color-gray-5)',
                                                backgroundColor: currentState.checkboxes.year
                                                    ? 'var(--mantine-color-red-0)'
                                                    : 'var(--mantine-color-gray-1)',
                                                color: 'var(--mantine-color-gray-7)',
                                            }
                                        }}
                                    />
                                </Input.Wrapper>
                            )}
                            <Box style={{flex: 1}}>
                                <NumberInput
                                    label={hasMetadata && currentState.metadata?.year ? "Year (new)" : "Year"}
                                    placeholder="Release year"
                                    value={currentState.form.year}
                                    onChange={(value) => handleFormChange({year: value as number | undefined})}
                                    disabled={hasMetadata && !!currentState.metadata?.year && !currentState.checkboxes.year}
                                    min={1900}
                                    max={new Date().getFullYear() + 1}
                                    styles={hasMetadata && currentState.metadata?.year ? {
                                        input: {
                                            borderColor: currentState.checkboxes.year
                                                ? 'var(--mantine-color-green-6)'
                                                : 'var(--mantine-color-gray-5)',
                                            backgroundColor: currentState.checkboxes.year
                                                ? 'var(--mantine-color-green-0)'
                                                : 'var(--mantine-color-gray-1)',
                                        }
                                    } : undefined}
                                />
                            </Box>
                        </Group>

                        <Group gap="xs" align="center">
                            {/* Rating doesn't have a metadata field in the diff, so no checkbox */}
                            <Input.Wrapper label="Rating" style={{flex: 1}}>
                                <Rating
                                    value={currentState.form.rating ?? 0}
                                    onChange={(value) => handleFormChange({rating: value})}
                                    fractions={2}
                                />
                            </Input.Wrapper>
                        </Group>

                        <Group gap="xs" align="flex-end">
                            {hasMetadata && currentState.metadata?.explicit && (
                                <Checkbox
                                    checked={currentState.checkboxes.explicit}
                                    onChange={(e) => handleCheckboxChange("explicit", e.currentTarget.checked)}
                                    mt={24}
                                />
                            )}
                            {hasMetadata && currentState.metadata?.explicit && (
                                <Input.Wrapper label="Explicit (old)">
                                    <Switch
                                        checked={currentState.metadata.explicit.old ?? false}
                                        readOnly
                                        styles={{
                                            input: {
                                                borderColor: currentState.checkboxes.explicit
                                                    ? 'var(--mantine-color-red-6)'
                                                    : 'var(--mantine-color-gray-5)',
                                                backgroundColor: currentState.checkboxes.explicit
                                                    ? 'var(--mantine-color-red-0)'
                                                    : 'var(--mantine-color-gray-1)',
                                            }
                                        }}
                                    />
                                </Input.Wrapper>
                            )}
                            <Box style={{flex: 1}}>
                                <Switch
                                    label={hasMetadata && currentState.metadata?.explicit ? "Explicit (new)" : "Explicit"}
                                    checked={currentState.form.explicit}
                                    onChange={(e) => handleFormChange({explicit: e.currentTarget.checked})}
                                    disabled={hasMetadata && !!currentState.metadata?.explicit && !currentState.checkboxes.explicit}
                                    styles={hasMetadata && currentState.metadata?.explicit ? {
                                        input: {
                                            borderColor: currentState.checkboxes.explicit
                                                ? 'var(--mantine-color-green-6)'
                                                : 'var(--mantine-color-gray-5)',
                                            backgroundColor: currentState.checkboxes.explicit
                                                ? 'var(--mantine-color-green-0)'
                                                : 'var(--mantine-color-gray-1)',
                                        }
                                    } : undefined}
                                />
                            </Box>
                        </Group>

                        <Group gap="xs" align="flex-end">
                            {hasMetadata && currentState.metadata?.lyrics && (
                                <Checkbox
                                    checked={currentState.checkboxes.lyrics}
                                    onChange={(e) => handleCheckboxChange("lyrics", e.currentTarget.checked)}
                                    mt={24}
                                />
                            )}
                            {hasMetadata && currentState.metadata?.lyrics && (
                                <Input.Wrapper label="Lyrics (old)">
                                    <Textarea
                                        value={currentState.metadata.lyrics.old?.substring(0, 200) ?? ""}
                                        readOnly
                                        rows={2}
                                        styles={{
                                            input: {
                                                borderColor: currentState.checkboxes.lyrics
                                                    ? 'var(--mantine-color-red-6)'
                                                    : 'var(--mantine-color-gray-5)',
                                                backgroundColor: currentState.checkboxes.lyrics
                                                    ? 'var(--mantine-color-red-0)'
                                                    : 'var(--mantine-color-gray-1)',
                                                color: 'var(--mantine-color-gray-7)',
                                            }
                                        }}
                                    />
                                </Input.Wrapper>
                            )}
                            <Box style={{flex: 1}}>
                                <Textarea
                                    label={hasMetadata && currentState.metadata?.lyrics ? "Lyrics (new)" : "Lyrics"}
                                    placeholder="Song lyrics"
                                    value={currentState.form.lyrics}
                                    onChange={(e) => handleFormChange({lyrics: e.target.value})}
                                    rows={5}
                                    disabled={hasMetadata && !!currentState.metadata?.lyrics && !currentState.checkboxes.lyrics}
                                    styles={hasMetadata && currentState.metadata?.lyrics ? {
                                        input: {
                                            borderColor: currentState.checkboxes.lyrics
                                                ? 'var(--mantine-color-green-6)'
                                                : 'var(--mantine-color-gray-5)',
                                            backgroundColor: currentState.checkboxes.lyrics
                                                ? 'var(--mantine-color-green-0)'
                                                : 'var(--mantine-color-gray-1)',
                                        }
                                    } : undefined}
                                />
                            </Box>
                        </Group>

                        <CoverUploadField
                            value={currentState.form.cover}
                            onChange={(value, dimensions) => handleFormChange({
                                cover: value,
                                coverDimensions: dimensions,
                            })}
                            currentCoverId={currentState.song.cover}
                            currentDimensions={currentState.form.coverDimensions}
                            diffMode={hasMetadata && !!currentState.metadata?.cover}
                            isChecked={currentState.checkboxes.cover}
                            onCheckChange={(checked) => handleCheckboxChange("cover", checked)}
                            oldCoverUrl={currentState.metadata?.cover?.old != null ? currentState.metadata.cover.old : undefined}
                        />
                    </Stack>
                </ScrollArea>

                <Group justify="space-between" mt="md">
                    <Group gap="xs">
                        <ActionIcon
                            variant="light"
                            size="lg"
                            onClick={handleAutoFetchMetadata}
                            disabled={isLoading}
                            loading={fetchMetadata.isPending}
                            title="Auto-fetch metadata"
                        >
                            <IconRefresh/>
                        </ActionIcon>
                        <ActionIcon
                            variant="light"
                            size="lg"
                            onClick={() => setMetadataSearchOpened(true)}
                            disabled={isLoading}
                            title="Search metadata"
                        >
                            <IconSearch/>
                        </ActionIcon>
                        {hasMetadata && (
                            <Text size="sm" c="dimmed">
                                {metadataFieldCount > 0
                                    ? `(${metadataFieldCount} diff${metadataFieldCount === 1 ? '' : 's'})`
                                    : '(0 diffs)'}
                            </Text>
                        )}
                    </Group>
                    <Group gap="xs">
                        <Button variant="subtle" onClick={handleClose} disabled={isLoading}>
                            Cancel
                        </Button>
                        {isMultiSong && (
                            <Button onClick={() => handleSaveCurrent(false)} loading={updateSong.isPending} disabled={isLoading}>
                                Save Current
                            </Button>
                        )}
                        <Button onClick={isMultiSong ? handleSaveAll : () => handleSaveCurrent(true)} loading={isLoading}>
                            {isMultiSong ? "Save All" : "Save"}
                        </Button>
                    </Group>
                </Group>
            </Modal>
        </>
    );
}
