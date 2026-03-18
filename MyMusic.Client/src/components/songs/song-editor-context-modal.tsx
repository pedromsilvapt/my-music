import {
    ActionIcon,
    Box,
    Button,
    Checkbox,
    Group,
    Input,
    NumberInput,
    Rating,
    ScrollArea,
    Skeleton,
    Stack,
    Switch,
    Text,
    Textarea,
    TextInput,
} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import type {ContextModalProps} from "@mantine/modals";
import {useQueryClient} from "@tanstack/react-query";
import {IconChevronLeft, IconChevronRight, IconRefresh, IconSearch} from "@tabler/icons-react";
import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {
    useBatchMultiUpdateSongs,
    useFetchSongMetadata,
    useUpdateSong,
    getSong,
} from "../../client/songs.ts";
import {useApplyMetadata} from "../../hooks/useApplyMetadata";
import {useAutoFetchMetadata, usePrefetchMetadata} from "../../hooks/useAutoFetchMetadata";
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
    hasChangesToSave,
    shouldSaveField,
    type FormState,
    type FieldCheckboxes,
} from "./song-edit-types";

interface SongEditorContextModalInnerProps {
    songIds: number[];
    onSuccess?: () => void;
}

interface SongEditState {
    song: GetSongResponseSong;
    metadata: SongMetadataDiff | null;
    form: FormState;
    checkboxes: FieldCheckboxes;
}

function createSongEditState(
    song: GetSongResponseSong, 
    metadata?: SongMetadataDiff | null,
    preSelectedFields?: string[]
): SongEditState {
    const form = metadata ? formStateFromMetadata(metadata, formStateFromSong(song)) : formStateFromSong(song);
    let checkboxes = metadata ? checkboxesFromMetadata(metadata) : createInitialCheckboxes();
    
    // Apply rule-based pre-selections
    if (preSelectedFields && preSelectedFields.length > 0) {
        checkboxes = {
            ...checkboxes,
            ...Object.fromEntries(
                preSelectedFields.map(field => [field, true])
            )
        } as FieldCheckboxes;
    }
    
    return { song, metadata: metadata ?? null, form, checkboxes };
}

export default function SongEditorContextModal({
    context,
    id,
    innerProps,
}: ContextModalProps<SongEditorContextModalInnerProps>) {
    const [songs, setSongs] = useState<GetSongResponseSong[]>([]);
    const [loading, setLoading] = useState(true);
    const [editStates, setEditStates] = useState<Map<number, SongEditState>>(new Map());
    const [currentIndex, setCurrentIndex] = useState(0);
    const [metadataSearchOpened, setMetadataSearchOpened] = useState(false);
    const shouldCloseAfterSave = useRef(false);
    const savedSongIds = useRef<Set<number>>(new Set());
    const queryClient = useQueryClient();
    const applyMetadata = useApplyMetadata();
    const {prefetch, getCached} = usePrefetchMetadata();
    
    // Track which songs we've attempted to load metadata for (either loaded or confirmed no metadata)
    const [metadataAttemptedSongs, setMetadataAttemptedSongs] = useState<Set<number>>(new Set());
    
    // Fetch auto-fetched metadata for the current song
    const currentSongId = songs[currentIndex]?.id ?? null;
    const metadataQuery = useAutoFetchMetadata(currentSongId);
    
    // Prefetch-ahead: when on song N, preload metadata for song N+1
    useEffect(() => {
        if (songs.length > 1 && currentIndex < songs.length - 1) {
            const nextSongId = songs[currentIndex + 1]?.id;
            if (nextSongId) {
                prefetch(nextSongId);
            }
        }
    }, [currentIndex, songs, prefetch]);

    const isMultiSong = songs.length > 1;
    const currentState = currentIndex < songs.length ? editStates.get(songs[currentIndex]?.id) : null;

    const modifiedSongIds = useMemo(() => {
        const modified = new Set<number>();
        editStates.forEach((state, id) => {
            if (savedSongIds.current.has(id)) return;
            if (hasChangesToSave(state.form, state.song, state.metadata, state.checkboxes)) {
                modified.add(id);
            }
        });
        return modified;
    }, [editStates]);

    const modifiedCountUpToCurrent = useMemo(() => {
        return songs.slice(0, currentIndex + 1)
            .filter(song => modifiedSongIds.has(song.id))
            .length;
    }, [songs, currentIndex, modifiedSongIds]);

    // Fetch songs on mount
    useEffect(() => {
        const fetchSongs = async () => {
            setLoading(true);
            const fetched: GetSongResponseSong[] = [];
            for (const songId of innerProps.songIds) {
                const response = await getSong(songId);
                if (response.data.song) {
                    fetched.push(response.data.song);
                }
            }
            setSongs(fetched);
            setLoading(false);
        };
        fetchSongs();
    }, [innerProps.songIds]);

    // Initialize edit states when songs and metadata are loaded for current song only
    useEffect(() => {
        if (!loading && songs.length > 0 && currentSongId) {
            // Skip if current song already initialized (from cache or previous navigation)
            if (editStates.has(currentSongId)) {
                return;
            }
            
            // If metadata query is still loading, wait (loading indicator shown on buttons)
            if (metadataQuery.isLoading) {
                return;
            }
            
            // Metadata loaded (or confirmed no metadata) - initialize current song
            setEditStates(prev => {
                const newMap = new Map(prev);
                const currentSong = songs.find(s => s.id === currentSongId);
                
                if (currentSong) {
                    const songMetadata = metadataQuery.data?.hasMetadata 
                        ? (metadataQuery.data.metadata as SongMetadataDiff) 
                        : null;
                    const preSelectedFields = metadataQuery.data?.preSelectedFields;
                    newMap.set(currentSong.id, createSongEditState(currentSong, songMetadata, preSelectedFields));
                }
                
                return newMap;
            });
            
            // Mark this song as attempted (either has metadata or confirmed no metadata)
            setMetadataAttemptedSongs(prev => new Set(prev).add(currentSongId));
            
            // Prefetch next song if not already attempted
            if (currentIndex < songs.length - 1) {
                const nextSongId = songs[currentIndex + 1]?.id;
                if (nextSongId && !metadataAttemptedSongs.has(nextSongId) && !editStates.has(nextSongId)) {
                    prefetch(nextSongId);
                }
            }
        }
    }, [loading, metadataQuery.isLoading, metadataQuery.data, songs, currentSongId, currentIndex, prefetch, metadataAttemptedSongs, editStates]);

    const handleClose = () => {
        setSongs([]);
        setEditStates(new Map());
        setCurrentIndex(0);
        setLoading(true);
        context.closeModal(id);
    };

    const updateSong = useUpdateSong({
        mutation: {
            onSuccess: (response, variables) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    const errorDetail = responseData?.detail || "Unknown error";
                    notifications.show({title: "Error", message: `Failed to update song: ${errorDetail}`, color: "red"});
                    return;
                }

                const savedSongId = variables.data.songId;
                savedSongIds.current.add(savedSongId);

                // Get the state for this song to check if auto-fetched metadata was used
                const savedState = editStates.get(savedSongId);
                const hadAutoFetchedMetadata = savedState?.metadata != null;

                setEditStates(prev => {
                    const newMap = new Map(prev);
                    const state = newMap.get(savedSongId);
                    if (state) {
                        newMap.set(savedSongId, {
                            ...state,
                            metadata: null,
                        });
                    }
                    return newMap;
                });

                // If auto-fetched metadata was used, mark it as applied
                if (hadAutoFetchedMetadata) {
                    applyMetadata.mutate({songId: savedSongId});
                }

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
                    const responseData = response.data as { detail?: string } | undefined;
                    const errorDetail = responseData?.detail || "Unknown error";
                    notifications.show({title: "Error", message: `Failed to update songs: ${errorDetail}`, color: "red"});
                    return;
                }

                const successfulSongs = response.data.songs
                    .filter((s: BatchMultiUpdateSongResult) => s.success)
                    .map((s: BatchMultiUpdateSongResult) => s.id);

                successfulSongs.forEach(id => { savedSongIds.current.add(id); });

                // Track which successful songs had auto-fetched metadata
                const songsWithAutoFetchedMetadata = successfulSongs.filter((id: number) => {
                    const state = editStates.get(id);
                    return state?.metadata != null;
                });

                setEditStates(prev => {
                    const newMap = new Map(prev);
                    successfulSongs.forEach((id: number) => {
                        const state = newMap.get(id);
                        if (state) {
                            newMap.set(id, {
                                ...state,
                                metadata: null,
                            });
                        }
                    });
                    return newMap;
                });

                // Mark auto-fetched metadata as applied for successful songs
                songsWithAutoFetchedMetadata.forEach((id: number) => {
                    applyMetadata.mutate({songId: id});
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
                    const responseData = response.data as { detail?: string } | undefined;
                    const errorDetail = responseData?.detail || "Unknown error";
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

    const handleMetadataFetched = useCallback((metadata: SongMetadataDiff) => {
        if (!currentState) return;

        const newForm = formStateFromMetadata(metadata, formStateFromSong(currentState.song));
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

    const handleSaveModifiedUpToCurrent = async () => {
        const songsToSave = songs.slice(0, currentIndex + 1).filter((song) => {
            return modifiedSongIds.has(song.id);
        });

        if (songsToSave.length === 0) {
            notifications.show({title: "Info", message: "No changes to save", color: "blue"});
            return;
        }

        const updates = songsToSave.map((song) => {
            const state = editStates.get(song.id)!;
            const { song: songData, form, checkboxes, metadata } = state;
            const update: Record<string, unknown> = { songId: songData.id };

            if (shouldSaveField(form, songData, metadata, checkboxes.title, "title") && form.title) {
                update.title = form.title;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.year, "year") && form.year !== undefined) {
                update.year = form.year;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.lyrics, "lyrics") && form.lyrics) {
                update.lyrics = form.lyrics;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.rating, "rating") && form.rating !== undefined) {
                update.rating = form.rating;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.explicit, "explicit")) {
                update.explicit = form.explicit;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.cover, "cover") && form.cover) {
                update.cover = form.cover;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.album, "album") && form.album) {
                if (form.album.id > 0) {
                    update.albumId = form.album.id;
                } else {
                    update.albumName = form.album.name;
                }
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.albumArtist, "albumArtist") && form.albumArtist) {
                if (form.albumArtist.id > 0) {
                    update.albumArtistId = form.albumArtist.id;
                } else {
                    update.albumArtistName = form.albumArtist.name;
                }
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.artists, "artists") && form.artists.length > 0) {
                update.artistIds = form.artists.filter(a => a.id > 0).map(a => a.id);
                update.artistNames = form.artists.filter(a => a.id < 0).map(a => a.name);
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.genres, "genres") && form.genres.length > 0) {
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

    const handleSaveCurrent = (closeAfterSave: boolean) => {
        if (!currentState) return;

        const { song, form, checkboxes, metadata } = currentState;

        const update: UpdateSongRequest = { songId: song.id };

        if (shouldSaveField(form, song, metadata, checkboxes.title, "title") && form.title) {
            update.title = form.title;
        }
        if (shouldSaveField(form, song, metadata, checkboxes.year, "year") && form.year !== undefined) {
            update.year = form.year;
        }
        if (shouldSaveField(form, song, metadata, checkboxes.lyrics, "lyrics") && form.lyrics) {
            update.lyrics = form.lyrics;
        }
        if (shouldSaveField(form, song, metadata, checkboxes.rating, "rating") && form.rating !== undefined) {
            update.rating = form.rating;
        }
        if (shouldSaveField(form, song, metadata, checkboxes.explicit, "explicit")) {
            update.explicit = form.explicit;
        }
        if (shouldSaveField(form, song, metadata, checkboxes.cover, "cover") && form.cover) {
            update.cover = form.cover;
        }
        if (shouldSaveField(form, song, metadata, checkboxes.album, "album") && form.album) {
            if (form.album.id > 0) {
                update.albumId = form.album.id;
            } else {
                update.albumName = form.album.name;
            }
        }
        if (shouldSaveField(form, song, metadata, checkboxes.albumArtist, "albumArtist") && form.albumArtist) {
            if (form.albumArtist.id > 0) {
                update.albumArtistId = form.albumArtist.id;
            } else {
                update.albumArtistName = form.albumArtist.name;
            }
        }
        if (shouldSaveField(form, song, metadata, checkboxes.artists, "artists") && form.artists.length > 0) {
            update.artistIds = form.artists.filter(a => a.id > 0).map(a => a.id);
            update.artistNames = form.artists.filter(a => a.id < 0).map(a => a.name);
        }
        if (shouldSaveField(form, song, metadata, checkboxes.genres, "genres") && form.genres.length > 0) {
            update.genreIds = form.genres.filter(g => g.id > 0).map(g => g.id);
            update.genreNames = form.genres.filter(g => g.id < 0).map(g => g.name);
        }

        if (Object.keys(update).length > 1) {
            shouldCloseAfterSave.current = closeAfterSave;
            updateSong.mutate({ id: currentState.song.id, data: update });
        } else {
            notifications.show({title: "Info", message: "No changes to save", color: "blue"});
        }
    };

    const handleSaveAll = () => {
        const songsToSave = songs.filter((song) => {
            return modifiedSongIds.has(song.id);
        });

        if (songsToSave.length === 0) {
            notifications.show({title: "Info", message: "No changes to save", color: "blue"});
            return;
        }

        const updates = songsToSave.map((song) => {
            const state = editStates.get(song.id)!;
            const { song: songData, form, checkboxes, metadata } = state;
            const update: Record<string, unknown> = { songId: songData.id };

            if (shouldSaveField(form, songData, metadata, checkboxes.title, "title") && form.title) {
                update.title = form.title;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.year, "year") && form.year !== undefined) {
                update.year = form.year;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.lyrics, "lyrics") && form.lyrics) {
                update.lyrics = form.lyrics;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.rating, "rating") && form.rating !== undefined) {
                update.rating = form.rating;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.explicit, "explicit")) {
                update.explicit = form.explicit;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.cover, "cover") && form.cover) {
                update.cover = form.cover;
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.album, "album") && form.album) {
                if (form.album.id > 0) {
                    update.albumId = form.album.id;
                } else {
                    update.albumName = form.album.name;
                }
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.albumArtist, "albumArtist") && form.albumArtist) {
                if (form.albumArtist.id > 0) {
                    update.albumArtistId = form.albumArtist.id;
                } else {
                    update.albumArtistName = form.albumArtist.name;
                }
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.artists, "artists") && form.artists.length > 0) {
                update.artistIds = form.artists.filter(a => a.id > 0).map(a => a.id);
                update.artistNames = form.artists.filter(a => a.id < 0).map(a => a.name);
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.genres, "genres") && form.genres.length > 0) {
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

    const handlePrevious = () => {
        if (currentIndex > 0) {
            // For back navigation, always use what's in editStates (already loaded)
            // No need to pre-initialize since we never remove songs from editStates
            setCurrentIndex(currentIndex - 1);
        }
    };

    const handleNext = () => {
        if (currentIndex < songs.length - 1) {
            const nextIndex = currentIndex + 1;
            const nextSongId = songs[nextIndex]?.id;
            
            // If we have cached metadata and song not yet initialized, pre-initialize it
            const cachedData = getCached(nextSongId);
            if (cachedData && !editStates.has(nextSongId)) {
                const nextSong = songs[nextIndex];
                setEditStates(prev => {
                    const newMap = new Map(prev);
                    const songMetadata = cachedData.hasMetadata 
                        ? (cachedData.metadata as SongMetadataDiff) 
                        : null;
                    newMap.set(nextSongId, createSongEditState(nextSong, songMetadata, cachedData.preSelectedFields));
                    return newMap;
                });
                setMetadataAttemptedSongs(prev => new Set(prev).add(nextSongId));
            }
            
            setCurrentIndex(nextIndex);
        }
    };

    const handleFormChange = (changes: Partial<FormState>) => {
        if (!currentState) return;

        setEditStates((prev) => {
            const newMap = new Map(prev);
            const state = newMap.get(currentState.song.id);
            if (state) {
                newMap.set(currentState.song.id, {
                    ...state,
                    form: { ...state.form, ...changes },
                });
            }
            return newMap;
        });
    };

    const handleCheckboxChange = (field: keyof FieldCheckboxes, checked: boolean) => {
        if (!currentState) return;

        setEditStates((prev) => {
            const newMap = new Map(prev);
            const state = newMap.get(currentState.song.id);
            if (state) {
                newMap.set(currentState.song.id, {
                    ...state,
                    checkboxes: { ...state.checkboxes, [field]: checked },
                });
            }
            return newMap;
        });
    };

    const hasMetadata = currentState?.metadata != null;
    const metadataFieldCount = useMemo(() => {
        if (!currentState?.metadata) return 0;
        return Object.values(currentState.metadata).filter(v => v != null).length;
    }, [currentState?.metadata]);

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

    const handleSelectAll = (checked: boolean) => {
        if (!currentState) return;

        setEditStates((prev) => {
            const newMap = new Map(prev);
            const state = newMap.get(currentState.song.id);
            if (state) {
                const newCheckboxes = { ...state.checkboxes };
                Object.keys(newCheckboxes).forEach((key) => {
                    newCheckboxes[key as keyof FieldCheckboxes] = checked;
                });
                newMap.set(currentState.song.id, {
                    ...state,
                    checkboxes: newCheckboxes,
                });
            }
            return newMap;
        });
    };

    const isLoading = updateSong.isPending || batchMultiUpdateSongs.isPending;

    if (loading) {
        return (
            <Stack gap="md" p="xl">
                <Skeleton height={40} radius="md" />
                <Skeleton height={60} radius="md" />
                <Skeleton height={60} radius="md" />
                <Skeleton height={100} radius="md" />
                <Skeleton height={40} radius="md" />
            </Stack>
        );
    }

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
                                    {currentState && !savedSongIds.current.has(currentState.song.id) && hasChangesToSave(currentState.form, currentState.song, currentState.metadata, currentState.checkboxes) && " *"}
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

                    <Group gap="xs" align="flex-start">
                        {hasMetadata && currentState.metadata?.title && (
                            <Checkbox
                                checked={currentState.checkboxes.title}
                                onChange={(e) => handleCheckboxChange("title", e.currentTarget.checked)}
                                mt={24}
                            />
                        )}
                        {hasMetadata && currentState.metadata?.title && (
                            <Box style={{flex: 1}}>
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
                            </Box>
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
                                        color: 'var(--mantine-color-gray-7)',
                                    }
                                } : undefined}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        {hasMetadata && currentState.metadata?.year && (
                            <Checkbox
                                checked={currentState.checkboxes.year}
                                onChange={(e) => handleCheckboxChange("year", e.currentTarget.checked)}
                                mt={24}
                            />
                        )}
                        {hasMetadata && currentState.metadata?.year && (
                            <Box style={{flex: 1}}>
                                <Input.Wrapper label="Year (old)">
                                    <Input
                                        value={currentState.metadata.year.old ?? ""}
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
                            </Box>
                        )}
                        <Box style={{flex: 1}}>
                            <NumberInput
                                label={hasMetadata && currentState.metadata?.year ? "Year (new)" : "Year"}
                                placeholder="Release year"
                                value={currentState.form.year}
                                onChange={(val) => handleFormChange({year: typeof val === 'number' ? val : undefined})}
                                disabled={hasMetadata && !!currentState.metadata?.year && !currentState.checkboxes.year}
                                styles={hasMetadata && currentState.metadata?.year ? {
                                    input: {
                                        borderColor: currentState.checkboxes.year
                                            ? 'var(--mantine-color-green-6)'
                                            : 'var(--mantine-color-gray-5)',
                                        backgroundColor: currentState.checkboxes.year
                                            ? 'var(--mantine-color-green-0)'
                                            : 'var(--mantine-color-gray-1)',
                                        color: 'var(--mantine-color-gray-7)',
                                    }
                                } : undefined}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        <Box style={{flex: 1}}>
                            <AutocompleteField
                                label="Album"
                                placeholder="Album name"
                                value={currentState.form.album?.name ? {id: currentState.form.album.id, name: currentState.form.album.name} : null}
                                onChange={(item) => handleFormChange({album: item && typeof item !== 'string' ? {id: item.id, name: item.name} : {id: 0, name: ""}})}
                                onSearch={searchAlbums}
                                disabled={hasMetadata && !!currentState.metadata?.album && !currentState.checkboxes.album}
                                diffMode={hasMetadata && !!currentState.metadata?.album}
                                originalValue={currentState.song.album ? { id: currentState.song.album.id, name: currentState.song.album.name } : null}
                                isChecked={currentState.checkboxes.album}
                                onCheckChange={(checked) => handleCheckboxChange("album", checked)}
                                originalDisplayValue={currentState.metadata?.album?.old?.name}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        <Box style={{flex: 1}}>
                            <AutocompleteField
                                label="Album Artist"
                                placeholder="Album artist"
                                value={currentState.form.albumArtist?.name ? {id: currentState.form.albumArtist.id, name: currentState.form.albumArtist.name} : null}
                                onChange={(item) => handleFormChange({albumArtist: item && typeof item !== 'string' ? {id: item.id, name: item.name} : {id: 0, name: ""}})}
                                onSearch={searchAlbums}
                                disabled={hasMetadata && !!currentState.metadata?.albumArtist && !currentState.checkboxes.albumArtist}
                                diffMode={hasMetadata && !!currentState.metadata?.albumArtist}
                                originalValue={currentState.song.album?.artist ? { id: currentState.song.album.artist.id, name: currentState.song.album.artist.name } : null}
                                isChecked={currentState.checkboxes.albumArtist}
                                onCheckChange={(checked) => handleCheckboxChange("albumArtist", checked)}
                                originalDisplayValue={currentState.metadata?.albumArtist?.old ?? undefined}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        <Box style={{flex: 1}}>
                            <TagsAutocompleteField
                                label="Artists"
                                value={currentState.form.artists}
                                onChange={(items) => handleFormChange({artists: items})}
                                onSearch={searchArtists}
                                placeholder="Add artist..."
                                disabled={hasMetadata && !!currentState.metadata?.artists && !currentState.checkboxes.artists}
                                diffMode={hasMetadata && !!currentState.metadata?.artists}
                                originalValue={currentState.song.artists.map(a => ({ id: a.id, name: a.name }))}
                                isChecked={currentState.checkboxes.artists}
                                onCheckChange={(checked) => handleCheckboxChange("artists", checked)}
                                originalDisplayValue={currentState.metadata?.artists?.old?.map(a => a.name).join(", ")}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        <Box style={{flex: 1}}>
                            <TagsAutocompleteField
                                label="Genres"
                                value={currentState.form.genres.map(g => ({id: g.id, name: g.name}))}
                                onChange={(items) => handleFormChange({genres: items})}
                                onSearch={searchGenres}
                                placeholder="Add genre..."
                                disabled={hasMetadata && !!currentState.metadata?.genres && !currentState.checkboxes.genres}
                                diffMode={hasMetadata && !!currentState.metadata?.genres}
                                originalValue={currentState.song.genres.map(g => ({ id: g.id, name: g.name }))}
                                isChecked={currentState.checkboxes.genres}
                                onCheckChange={(checked) => handleCheckboxChange("genres", checked)}
                                originalDisplayValue={currentState.metadata?.genres?.old?.join(", ")}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        {hasMetadata && currentState.metadata?.lyrics && (
                            <Checkbox
                                checked={currentState.checkboxes.lyrics}
                                onChange={(e) => handleCheckboxChange("lyrics", e.currentTarget.checked)}
                                mt={24}
                            />
                        )}
                        {hasMetadata && currentState.metadata?.lyrics && (
                            <Box style={{flex: 1}}>
                                <Input.Wrapper label="Lyrics (old)">
                                    <Textarea
                                        value={currentState.metadata.lyrics.old ?? ""}
                                        readOnly
                                        minRows={3}
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
                            </Box>
                        )}
                        <Box style={{flex: 1}}>
                            <Textarea
                                label={hasMetadata && currentState.metadata?.lyrics ? "Lyrics (new)" : "Lyrics"}
                                placeholder="Song lyrics"
                                value={currentState.form.lyrics}
                                onChange={(e) => handleFormChange({lyrics: e.target.value})}
                                minRows={3}
                                disabled={hasMetadata && !!currentState.metadata?.lyrics && !currentState.checkboxes.lyrics}
                                styles={hasMetadata && currentState.metadata?.lyrics ? {
                                    input: {
                                        borderColor: currentState.checkboxes.lyrics
                                            ? 'var(--mantine-color-green-6)'
                                            : 'var(--mantine-color-gray-5)',
                                        backgroundColor: currentState.checkboxes.lyrics
                                            ? 'var(--mantine-color-green-0)'
                                            : 'var(--mantine-color-gray-1)',
                                        color: 'var(--mantine-color-gray-7)',
                                    }
                                } : undefined}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        {hasMetadata && currentState.metadata?.rating && (
                            <Checkbox
                                checked={currentState.checkboxes.rating}
                                onChange={(e) => handleCheckboxChange("rating", e.currentTarget.checked)}
                                mt={24}
                            />
                        )}
                        {hasMetadata && currentState.metadata?.rating && (
                            <Box style={{flex: 1}}>
                                <Input.Wrapper label="Rating (old)">
                                    <Rating
                                        value={currentState.metadata.rating.old ?? 0}
                                        readOnly
                                        styles={{
                                            root: {
                                                borderColor: currentState.checkboxes.rating
                                                    ? 'var(--mantine-color-red-6)'
                                                    : 'var(--mantine-color-gray-5)',
                                            }
                                        }}
                                    />
                                </Input.Wrapper>
                            </Box>
                        )}
                        <Box style={{flex: 1}}>
                            <Input.Wrapper 
                                label={hasMetadata && currentState.metadata?.rating ? "Rating (new)" : "Rating"}
                            >
                                <Rating
                                    value={currentState.form.rating ?? 0}
                                    onChange={(val) => handleFormChange({rating: val})}
                                    readOnly={hasMetadata && !!currentState.metadata?.rating && !currentState.checkboxes.rating}
                                />
                            </Input.Wrapper>
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        {hasMetadata && currentState.metadata?.explicit != undefined && (
                            <Checkbox
                                checked={currentState.checkboxes.explicit}
                                onChange={(e) => handleCheckboxChange("explicit", e.currentTarget.checked)}
                                mt={24}
                            />
                        )}
                        {hasMetadata && currentState.metadata?.explicit != undefined && (
                            <Box style={{flex: 1}}>
                                <Input.Wrapper label="Explicit (old)">
                                    <Switch
                                        checked={currentState.metadata.explicit?.old ?? false}
                                        readOnly
                                        label={currentState.metadata.explicit?.old ? "Yes" : "No"}
                                        styles={{
                                            track: {
                                                borderColor: currentState.checkboxes.explicit
                                                    ? 'var(--mantine-color-red-6)'
                                                    : 'var(--mantine-color-gray-5)',
                                            }
                                        }}
                                    />
                                </Input.Wrapper>
                            </Box>
                        )}
                        <Box style={{flex: 1}}>
                            <Switch
                                label={hasMetadata && currentState.metadata?.explicit != undefined ? "Explicit (new)" : "Explicit"}
                                checked={currentState.form.explicit}
                                onChange={(e) => handleFormChange({explicit: e.currentTarget.checked})}
                                disabled={hasMetadata && currentState.metadata?.explicit != undefined && !currentState.checkboxes.explicit}
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        <Box style={{flex: 1}}>
                            <CoverUploadField
                                label={hasMetadata && currentState.metadata?.cover ? "Cover (new)" : "Cover"}
                                value={currentState.form.cover || ""}
                                onChange={(val, dimensions) => handleFormChange({cover: val, coverDimensions: dimensions})}
                                currentCoverId={currentState.song.cover}
                                currentDimensions={currentState.form.coverDimensions}
                                diffMode={hasMetadata && !!currentState.metadata?.cover}
                                isChecked={currentState.checkboxes.cover}
                                onCheckChange={(checked) => handleCheckboxChange("cover", checked)}
                                oldCoverUrl={currentState.metadata?.cover?.old ?? undefined}
                                disabled={hasMetadata && !!currentState.metadata?.cover && !currentState.checkboxes.cover}
                            />
                        </Box>
                    </Group>

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
                </Group>
                <Group gap="xs">
                    <Button variant="subtle" onClick={handleClose}>
                        Cancel
                    </Button>
                    {isMultiSong && (
                        <Button 
                            onClick={() => {
                                handleSaveModifiedUpToCurrent();
                                if (modifiedCountUpToCurrent > 0 && currentIndex < songs.length - 1) {
                                    setCurrentIndex(currentIndex + 1);
                                }
                            }} 
                            loading={batchMultiUpdateSongs.isPending} 
                            disabled={isLoading || modifiedCountUpToCurrent === 0}
                        >
                            Save Modified ({modifiedCountUpToCurrent})
                        </Button>
                    )}
                    <Button onClick={isMultiSong ? handleSaveAll : () => handleSaveCurrent(true)} loading={isLoading} disabled={isMultiSong ? false : modifiedCountUpToCurrent === 0}>
                        {isMultiSong ? "Save All" : "Save"}
                    </Button>
                </Group>
            </Group>
        </>
    );
}
