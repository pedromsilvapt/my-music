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
    useUpdateSong,
    autocompleteAlbums,
    autocompleteArtists,
    autocompleteGenres,
} from "../../client/songs.ts";
import {
    getLocalSong,
} from "../../client/songs.ts";
import {useApplyMetadata} from "../../hooks/useApplyMetadata";
import {useAutoFetchMetadata} from "../../hooks/useAutoFetchMetadata";
import {usePrefetchMetadata, useMetadataPrefetchAhead} from "../../hooks/usePrefetchMetadata";
import type {SongMetadataDiff} from "../../model/songMetadataDiff";
import type {GetSongResponseSong, UpdateSongRequest} from "../../model";
import type {BatchMultiUpdateSongResult} from "../../model/batchMultiUpdateSongResult";
import type {SongMultiUpdateItem} from "../../model/songMultiUpdateItem";
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

/**
 * Song Edit Modal — Metadata Fetching Behavior
 *
 * This modal has three metadata-fetch mechanisms:
 *
 * 1. Prefetch on modal open (database lookup):
 *    When the modal is opened from an audit rule page, auditRuleIds is provided.
 *    The usePrefetchMetadata hook reads previously auto-fetched metadata from the
 *    AutoFetchedMetadata database table (GET /metadata-fetch/song/{songId}, no source params).
 *    The server returns preSelectedFields based on the audit rules that flagged the song,
 *    which are used to auto-check the relevant diff checkboxes.
 *    When opened from a non-audit context (songs page, song detail), auditRuleIds is absent
 *    and the prefetch query stays disabled.
 *
 * 2. Auto button (live source search):
 *    The refresh icon button triggers useAutoFetchMetadata, which calls
 *    POST /songs/{id}/fetch-metadata. This searches all configured sources for the song,
 *    picks the best match, and fetches full metadata directly from that source.
 *    When in an audit context, the new metadata is merged with the existing audit-rule-based
 *    pre-selections (the preSelectedFields checkboxes are preserved).
 *
 * 3. Manual search (user picks a source result):
 *    The search icon button opens the MetadataSearchModal. The user searches across all sources,
 *    picks a result, and the modal calls GET /metadata-fetch/song/{songId}?sourceId=X&sourceSongId=Y
 *    to fetch directly from that chosen source. No audit-rule-based pre-selections are returned
 *    by the server for manual selections (preSelectedFields is empty).
 */
interface SongEditorContextModalInnerProps {
    songIds: number[];
    auditRuleIds?: number[];
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
    const {prefetch, getCached} = useMetadataPrefetchAhead();
    
    const isAuditContext = (innerProps.auditRuleIds?.length ?? 0) > 0;
    
    // Track which songs we've attempted to load metadata for (either loaded or confirmed no metadata)
    const [metadataAttemptedSongs, setMetadataAttemptedSongs] = useState<Set<number>>(new Set());
    
    // Prefetch metadata from the AutoFetchedMetadata DB table for the current song.
    // Only enabled when the modal was opened from an audit context (auditRuleIds provided).
    const currentSongId = songs[currentIndex]?.id ?? null;
    const metadataQuery = usePrefetchMetadata(currentSongId, isAuditContext);
    
    // Prefetch-ahead: when on song N, preload metadata for song N+1 (only in audit context)
    useEffect(() => {
        if (isAuditContext && songs.length > 1 && currentIndex < songs.length - 1) {
            const nextSongId = songs[currentIndex + 1]?.id;
            if (nextSongId) {
                prefetch(nextSongId);
            }
        }
    }, [isAuditContext, currentIndex, songs, prefetch]);

    const isMultiSong = songs.length > 1;
    const currentState = currentIndex < songs.length ? editStates.get(songs[currentIndex]?.id) : null;

    const albumArtistMismatch = useMemo(() => {
        if (!currentState) return null;
        
        const songArtists = currentState.form.artists;
        const albumArtist = currentState.form.albumArtist;
        
        if (!albumArtist?.name) return null;
        if (songArtists.length === 0) return null;
        
        let isMatch: boolean;
        
        if (albumArtist.id > 0) {
            isMatch = songArtists.some(a => a.id === albumArtist.id && a.id > 0);
        } else {
            isMatch = songArtists.some(a => a.id <= 0 && a.name === albumArtist.name);
        }
        
        return isMatch ? null : albumArtist.name;
    }, [currentState]);

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
                const response = await getLocalSong(songId);
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
            
            // In audit context, wait for the prefetch metadata query before initializing.
            // The query fetches from the AutoFetchedMetadata DB table and returns preSelectedFields.
            // In non-audit context, the query is disabled so we initialize immediately with no metadata.
            if (isAuditContext && metadataQuery.isLoading) {
                return;
            }
            
            const initializeCurrentSong = async () => {
                const currentSong = songs.find(s => s.id === currentSongId);
                
                if (currentSong) {
                    const responseData = metadataQuery.data?.data;
                    const songMetadata = responseData?.hasMetadata 
                        ? (responseData.metadata as SongMetadataDiff) 
                        : null;
                    const preSelectedFields = responseData?.preSelectedFields;
                    const editState = createSongEditState(currentSong, songMetadata, preSelectedFields);
                    
                    setEditStates(prev => {
                        const newMap = new Map(prev);
                        newMap.set(currentSong.id, editState);
                        return newMap;
                    });
                }
            };
            
            initializeCurrentSong();
            
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
    }, [loading, isAuditContext, metadataQuery.isLoading, metadataQuery.data, songs, currentSongId, currentIndex, prefetch, metadataAttemptedSongs, editStates]);

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

    const handleMetadataFetched = useCallback((metadata: SongMetadataDiff, preservePreSelections: boolean = false) => {
        if (!currentState) return;

        const newForm = formStateFromMetadata(metadata, formStateFromSong(currentState.song));
        let newCheckboxes = checkboxesFromMetadata(metadata);

        // When auto-fetching in an audit context, preserve any currently checked checkboxes
        // so that audit-rule-based pre-selections are not lost when metadata is refreshed.
        if (preservePreSelections) {
            const preservedSelections = Object.entries(currentState.checkboxes)
                .filter(([_, checked]) => checked)
                .map(([field, _]) => [field, true]);
            newCheckboxes = {
                ...newCheckboxes,
                ...Object.fromEntries(preservedSelections),
            } as FieldCheckboxes;
        }

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

    const {autoFetch, isPending: isAutoFetchPending} = useAutoFetchMetadata(
        useCallback((metadata: SongMetadataDiff) => handleMetadataFetched(metadata, isAuditContext), [handleMetadataFetched, isAuditContext]),
    );

    const handleAutoFetchMetadata = useCallback(() => {
        if (!currentState) return;
        autoFetch(currentState.song.id);
    }, [currentState, autoFetch]);

    const handleMetadataSelect = useCallback((metadata: SongMetadataDiff) => {
        handleMetadataFetched(metadata, false);
    }, [handleMetadataFetched]);

    const searchAlbums = useCallback(async (query: string): Promise<AutocompleteItem[]> => {
        const response = await autocompleteAlbums({ search: query });
        return response.data.albums.map((a) => ({
            id: a.id,
            name: a.name,
            subtitle: a.artistName ?? undefined,
            artistId: a.artistId,
            artistName: a.artistName,
            coverId: a.coverId,
        }));
    }, []);

    const searchArtists = useCallback(async (query: string): Promise<TagsAutocompleteItem[]> => {
        const response = await autocompleteArtists({ search: query });
        return response.data.artists.map((a) => ({
            id: a.id,
            name: a.name,
            coverId: a.coverId,
            albumCount: a.albumCount,
            songCount: a.songCount,
        }));
    }, []);

    const searchArtistsForAutocomplete = useCallback(async (query: string): Promise<AutocompleteItem[]> => {
        const response = await autocompleteArtists({ search: query });
        return response.data.artists.map((a) => ({
            id: a.id,
            name: a.name,
            coverId: a.coverId,
            subtitle: a.albumCount && a.songCount 
                ? `${a.albumCount} albums, ${a.songCount} songs`
                : undefined,
        }));
    }, []);

    const searchGenres = useCallback(async (query: string): Promise<TagsAutocompleteItem[]> => {
        const response = await autocompleteGenres({ search: query });
        return response.data.genres.map((g) => ({ id: g.id, name: g.name }));
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
            const update: SongMultiUpdateItem = { songId: songData.id };

            if (shouldSaveField(form, songData, metadata, checkboxes.title, "title")) {
                update.title = { newValue: form.title };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.year, "year")) {
                update.year = { newValue: form.year };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.lyrics, "lyrics")) {
                update.lyrics = { newValue: form.lyrics || null };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.rating, "rating")) {
                update.rating = { newValue: form.rating };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.explicit, "explicit")) {
                update.explicit = { newValue: form.explicit };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.cover, "cover")) {
                update.cover = { newValue: form.cover };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.album, "album") && form.album) {
                update.album = {
                    newValue: {
                        id: form.album.id > 0 ? form.album.id : null,
                        name: form.album.id < 0 ? form.album.name : null,
                        artistName: form.album.id < 0 ? form.albumArtist?.name : null,
                    },
                };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.artists, "artists")) {
                update.artists = {
                    newValue: form.artists.map(a => ({
                        id: a.id > 0 ? a.id : null,
                        name: a.id < 0 ? a.name : null,
                    })),
                };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.genres, "genres")) {
                update.genres = {
                    newValue: form.genres.map(g => ({
                        id: g.id > 0 ? g.id : null,
                        name: g.id < 0 ? g.name : null,
                    })),
                };
            }

            return update;
        });

        batchMultiUpdateSongs.mutate({
            data: { updates },
        });
    };

    const handleSaveCurrent = (closeAfterSave: boolean) => {
        if (!currentState) return;

        const { song, form, checkboxes, metadata } = currentState;

        const update: UpdateSongRequest = { songId: song.id };

        if (shouldSaveField(form, song, metadata, checkboxes.title, "title")) {
            update.title = { newValue: form.title };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.year, "year")) {
            update.year = { newValue: form.year };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.lyrics, "lyrics")) {
            update.lyrics = { newValue: form.lyrics || null };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.rating, "rating")) {
            update.rating = { newValue: form.rating };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.explicit, "explicit")) {
            update.explicit = { newValue: form.explicit };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.cover, "cover")) {
            update.cover = { newValue: form.cover };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.album, "album") && form.album) {
            update.album = {
                newValue: {
                    id: form.album.id > 0 ? form.album.id : null,
                    name: form.album.id < 0 ? form.album.name : null,
                    artistName: form.album.id < 0 ? form.albumArtist?.name : null,
                },
            };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.artists, "artists")) {
            update.artists = {
                newValue: form.artists.map(a => ({
                    id: a.id > 0 ? a.id : null,
                    name: a.id < 0 ? a.name : null,
                })),
            };
        }
        if (shouldSaveField(form, song, metadata, checkboxes.genres, "genres")) {
            update.genres = {
                newValue: form.genres.map(g => ({
                    id: g.id > 0 ? g.id : null,
                    name: g.id < 0 ? g.name : null,
                })),
            };
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
            const update: SongMultiUpdateItem = { songId: songData.id };

            if (shouldSaveField(form, songData, metadata, checkboxes.title, "title")) {
                update.title = { newValue: form.title };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.year, "year")) {
                update.year = { newValue: form.year };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.lyrics, "lyrics")) {
                update.lyrics = { newValue: form.lyrics || null };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.rating, "rating")) {
                update.rating = { newValue: form.rating };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.explicit, "explicit")) {
                update.explicit = { newValue: form.explicit };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.cover, "cover")) {
                update.cover = { newValue: form.cover };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.album, "album") && form.album) {
                update.album = {
                    newValue: {
                        id: form.album.id > 0 ? form.album.id : null,
                        name: form.album.id < 0 ? form.album.name : null,
                        artistName: form.album.id < 0 ? form.albumArtist?.name : null,
                    },
                };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.artists, "artists")) {
                update.artists = {
                    newValue: form.artists.map(a => ({
                        id: a.id > 0 ? a.id : null,
                        name: a.id < 0 ? a.name : null,
                    })),
                };
            }
            if (shouldSaveField(form, songData, metadata, checkboxes.genres, "genres")) {
                update.genres = {
                    newValue: form.genres.map(g => ({
                        id: g.id > 0 ? g.id : null,
                        name: g.id < 0 ? g.name : null,
                    })),
                };
            }

            return update;
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
                const songMetadata = cachedData.hasMetadata 
                    ? (cachedData.metadata as SongMetadataDiff) 
                    : null;
                const editState = createSongEditState(nextSong, songMetadata, cachedData.preSelectedFields);
                setEditStates(prev => {
                    const newMap = new Map(prev);
                    newMap.set(nextSongId, editState);
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

    const handleAlbumChange = useCallback((item: AutocompleteItem | string | null) => {
        if (item && typeof item !== 'string') {
            const albumUpdate = {
                id: item.id,
                name: item.name,
                artistId: item.artistId,
                artistName: item.artistName,
                coverId: item.coverId
            };
            const albumArtistUpdate = item.artistId && item.artistName
                ? {id: item.artistId, name: item.artistName}
                : undefined;
            handleFormChange({
                album: albumUpdate,
                ...(albumArtistUpdate !== undefined && {albumArtist: albumArtistUpdate})
            });
        } else {
            handleFormChange({album: {id: 0, name: ""}});
        }
    }, [handleFormChange]);

    const hasMetadata = currentState?.metadata != null;
    
    const isAlbumArtistDisabled = useMemo(() => {
        if (!currentState) return true;
        
        if (hasMetadata && !!currentState.metadata?.albumArtist && !currentState.checkboxes.albumArtist) {
            return true;
        }
        
        const albumId = currentState.form.album?.id;
        if (albumId && albumId > 0) {
            return true;
        }
        
        return false;
    }, [hasMetadata, currentState]);
    
    const metadataFieldCount = useMemo(() => {
        if (!currentState?.metadata) return 0;
        return Object.values(currentState.metadata).filter(v => v != null).length;
    }, [currentState?.metadata]);

    const lyricsMinRows = useMemo(() => {
        const oldRows = (currentState?.metadata?.lyrics?.old ?? '').split('\n').length;
        const newRows = (currentState?.form?.lyrics ?? '').split('\n').length;
        return Math.min(12, Math.max(3, Math.max(oldRows, newRows)));
    }, [currentState?.metadata?.lyrics, currentState?.form?.lyrics]);

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
                                data-testid="edit-song-title"
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
                                value={currentState.form.year ?? undefined}
                                onChange={(val) => handleFormChange({year: typeof val === 'number' ? val : undefined})}
                                disabled={hasMetadata && !!currentState.metadata?.year && !currentState.checkboxes.year}
                                data-testid="edit-song-year"
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
                                onChange={handleAlbumChange}
                                onSearch={searchAlbums}
                                showArtwork
                                testId="edit-song-album"
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
                                onSearch={searchArtistsForAutocomplete}
                                showArtwork
                                testId="edit-song-album-artist"
                                error={albumArtistMismatch ? `Not in the song's artists list` : undefined}
                                disabled={isAlbumArtistDisabled}
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
                                showArtwork
                                testId="edit-song-artists"
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
                                testId="edit-song-genres"
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
                                        minRows={lyricsMinRows}
                                        autosize
                                        maxRows={12}
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
                                minRows={lyricsMinRows}
                                autosize
                                maxRows={12}
                                disabled={hasMetadata && !!currentState.metadata?.lyrics && !currentState.checkboxes.lyrics}
                                data-testid="edit-song-lyrics"
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
                                        fractions={2}
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
                                data-testid="edit-song-rating"
                            >
                                <Rating
                                    fractions={2}
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
                                data-testid="edit-song-explicit"
                            />
                        </Box>
                    </Group>

                    <Group gap="xs" align="flex-start">
                        <Box style={{flex: 1}}>
                            <CoverUploadField
                                label={hasMetadata && currentState.metadata?.cover ? "Cover (new)" : "Cover"}
                                value={currentState.form.cover}
                                onChange={(val, dimensions) => handleFormChange({cover: val, coverDimensions: dimensions, coverUrl: undefined})}
                                currentDimensions={currentState.form.coverDimensions}
                                diffMode={hasMetadata && !!currentState.metadata?.cover}
                                isChecked={currentState.checkboxes.cover}
                                onCheckChange={(checked) => handleCheckboxChange("cover", checked)}
                                oldCoverUrl={currentState.metadata?.cover?.old ?? undefined}
                                coverUrl={currentState.form.coverUrl}
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
                        loading={isAutoFetchPending}
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
