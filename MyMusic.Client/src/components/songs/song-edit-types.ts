import type { GetSongResponseSong } from "../../model/getSongResponseSong";
import type {
    SongMetadataDiff,
} from "../../model/songMetadataDiff";

export interface SongEditMetadata {
    title?: string;
    year?: number;
    lyrics?: string;
    rating?: number;
    explicit?: boolean;
    cover?: string;
    album?: { id?: number; name: string; artistName?: string };
    albumArtist?: { id?: number; name: string };
    artists?: Array<{ id?: number; name: string }>;
    genres?: Array<string>;
}

export interface FieldCheckboxes {
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

export interface AutocompleteItem {
    id: number;
    name: string;
    subtitle?: string;
}

export interface TagsAutocompleteItem {
    id: number;
    name: string;
}

export interface FormState {
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

export interface SongEditState {
    song: GetSongResponseSong;
    metadata: SongMetadataDiff | null;
    form: FormState;
    checkboxes: FieldCheckboxes;
    originalForm: FormState;
}

export function createInitialFormState(): FormState {
    return {
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
}

export function createInitialCheckboxes(): FieldCheckboxes {
    return {
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
}

export function formStateFromSong(song: GetSongResponseSong): FormState {
    return {
        title: song.title,
        year: song.year ?? undefined,
        lyrics: song.lyrics ?? "",
        rating: song.rating ?? undefined,
        explicit: song.isExplicit,
        cover: null,
        coverDimensions: song.coverDetails
            ? { width: song.coverDetails.width, height: song.coverDetails.height }
            : null,
        album: song.album ? { id: song.album.id, name: song.album.name } : null,
        albumArtist: song.album?.artist
            ? { id: song.album.artist.id, name: song.album.artist.name }
            : null,
        artists: song.artists.map((a: { id: number; name: string }) => ({ id: a.id, name: a.name })),
        genres: song.genres.map((g: { id: number; name: string }) => ({ id: g.id, name: g.name })),
    };
}

export function formStateFromMetadata(
    metadata: SongMetadataDiff,
    originalForm: FormState,
): FormState {
    const form: FormState = { ...originalForm };

    if (metadata.title) {
        form.title = metadata.title.new ?? "";
    }
    if (metadata.year) {
        form.year = metadata.year.new ?? undefined;
    }
    if (metadata.lyrics) {
        form.lyrics = metadata.lyrics.new ?? "";
    }
    if (metadata.rating) {
        form.rating = metadata.rating.new ?? undefined;
    }
    if (metadata.explicit) {
        form.explicit = metadata.explicit.new ?? false;
    }
    if (metadata.cover) {
        form.cover = metadata.cover.new ?? null;
    }
    if (metadata.album) {
        form.album = {
            id: -1,
            name: metadata.album.new?.name ?? "",
        };
        if (metadata.album.new?.artistName) {
            form.albumArtist = {
                id: -1,
                name: metadata.album.new.artistName,
            };
        }
    }
    if (metadata.artists) {
        form.artists = (metadata.artists.new ?? []).map((a: { name?: string }) => ({
            id: -1,
            name: a.name ?? "",
        }));
    }
    if (metadata.genres) {
        form.genres = (metadata.genres.new ?? []).map((name: string) => ({
            id: -1,
            name,
        }));
    }

    return form;
}

export function checkboxesFromMetadata(metadata: SongMetadataDiff | null): FieldCheckboxes {
    if (!metadata) {
        return createInitialCheckboxes();
    }

    return {
        title: !!metadata.title,
        year: !!metadata.year,
        lyrics: !!metadata.lyrics,
        rating: !!metadata.rating,
        explicit: !!metadata.explicit,
        cover: !!metadata.cover,
        album: !!metadata.album,
        albumArtist: !!(metadata as any).albumArtist,
        artists: !!metadata.artists,
        genres: !!metadata.genres,
    };
}

export function getMetadataFieldValue<K extends keyof SongMetadataDiff>(
    metadata: SongMetadataDiff,
    field: K,
): { old: unknown; new: unknown } | null {
    const value = metadata[field];
    if (value == null) return null;
    return { old: (value as { old: unknown }).old, new: (value as { new: unknown }).new };
}

export function hasPendingChanges(state: SongEditState): boolean {
    if (state.metadata != null) {
        return true;
    }
    
    if (state.form.title !== state.originalForm.title) return true;
    if (state.form.year !== state.originalForm.year) return true;
    if (state.form.lyrics !== state.originalForm.lyrics) return true;
    if (state.form.rating !== state.originalForm.rating) return true;
    if (state.form.explicit !== state.originalForm.explicit) return true;
    if (state.form.cover !== state.originalForm.cover) return true;
    
    const formAlbumId = state.form.album?.id ?? 0;
    const origAlbumId = state.originalForm.album?.id ?? 0;
    if (formAlbumId !== origAlbumId) return true;
    if (state.form.album?.name !== state.originalForm.album?.name) return true;
    
    const formAlbumArtistId = state.form.albumArtist?.id ?? 0;
    const origAlbumArtistId = state.originalForm.albumArtist?.id ?? 0;
    if (formAlbumArtistId !== origAlbumArtistId) return true;
    if (state.form.albumArtist?.name !== state.originalForm.albumArtist?.name) return true;
    
    const formArtistIds = state.form.artists.map(a => a.id).sort().join(",");
    const origArtistIds = state.originalForm.artists.map(a => a.id).sort().join(",");
    if (formArtistIds !== origArtistIds) return true;
    
    const formGenreIds = state.form.genres.map(g => g.id).sort().join(",");
    const origGenreIds = state.originalForm.genres.map(g => g.id).sort().join(",");
    if (formGenreIds !== origGenreIds) return true;
    
    return false;
}