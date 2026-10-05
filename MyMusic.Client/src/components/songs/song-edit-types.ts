import type { GetSongResponseSong } from "../../model/getSongResponseSong";
import type { ArtworkRef } from "../../model/artworkRef";
import type {
    SongMetadataDiff,
} from "../../model/songMetadataDiff";
import type { ValueUpdateOfAlbumRef } from "../../model/valueUpdateOfAlbumRef";
import type { AutocompleteItem } from "./autocomplete-field";
import type { TagsAutocompleteItem } from "./tags-autocomplete-field";

export { type AutocompleteItem, type TagsAutocompleteItem };

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

export interface FormState {
    title: string;
    year: number | null;
    lyrics: string;
    rating: number | null;
    explicit: boolean;
    cover: ArtworkRef | null;
    coverUrl?: string;
    coverDimensions: { width: number; height: number } | null;
    /** An album is edited by name only: on save, it is found or created among the albums of the album artist. */
    album: string;
    albumArtist: AutocompleteItem | null;
    artists: TagsAutocompleteItem[];
    genres: TagsAutocompleteItem[];
}

export interface SongEditState {
    song: GetSongResponseSong;
    metadata: SongMetadataDiff | null;
    form: FormState;
    checkboxes: FieldCheckboxes;
}

export function createInitialFormState(): FormState {
    return {
        title: "",
        year: null,
        lyrics: "",
        rating: null,
        explicit: false,
        cover: null,
        coverDimensions: null,
        album: "",
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
        year: song.year ?? null,
        lyrics: song.lyrics ?? "",
        rating: song.rating ?? null,
        explicit: song.isExplicit,
        cover: song.cover ? { id: song.cover } : null,
        coverDimensions: song.coverDetails
            ? { width: song.coverDetails.width, height: song.coverDetails.height }
            : null,
        album: song.album?.name ?? "",
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
    // Always clear coverUrl first, then set if metadata has cover
    form.coverUrl = undefined;
    if (metadata.cover) {
        if (metadata.cover.new) {
            form.coverUrl = metadata.cover.new;
            form.cover = null;
        } else {
            form.cover = null;
        }
    }
    if (metadata.album) {
        form.album = metadata.album.new?.name ?? "";
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
        albumArtist: !!metadata.albumArtist,
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

export function hasFieldMetadataDiff(
    metadata: SongMetadataDiff | null,
    field: keyof FieldCheckboxes
): boolean {
    if (!metadata) return false;
    if (field === "albumArtist") {
        return !!(metadata.album?.new?.artistName || metadata.albumArtist);
    }
    return metadata[field as keyof SongMetadataDiff] != null;
}

export function isFieldDifferentFromSong(
    form: FormState,
    song: GetSongResponseSong,
    field: keyof FormState
): boolean {
    switch (field) {
        case "title":
            return form.title !== song.title;
        case "year":
            if (form.year === null && song.year != null) return true;
            if (form.year !== null && song.year == null) return true;
            return form.year !== song.year;
        case "lyrics": {
            const formLyrics = form.lyrics || null;
            const songLyrics = song.lyrics || null;
            return formLyrics !== songLyrics;
        }
        case "rating":
            if (form.rating === null && song.rating != null) return true;
            if (form.rating !== null && song.rating == null) return true;
            return form.rating !== song.rating;
        case "explicit":
            return form.explicit !== song.isExplicit;
        case "cover":
            if (form.cover === null && song.cover == null) return false;
            if (form.cover?.id != null && form.cover.id === song.cover) return false;
            return true;
        case "album":
            return form.album !== (song.album?.name ?? "");
        case "albumArtist": {
            const songAlbumArtist = song.album?.artist;
            if (!form.albumArtist && !songAlbumArtist) return false;
            if (!form.albumArtist || !songAlbumArtist) return true;
            if (form.albumArtist.id > 0) {
                return form.albumArtist.id !== songAlbumArtist.id;
            }
            return form.albumArtist.name !== songAlbumArtist.name;
        }
        case "artists": {
            const getKey = (a: {id: number; name: string}) => a.id > 0 ? `id:${a.id}` : `name:${a.name}`;
            const formKeys = form.artists.map(getKey).sort().join(",");
            const songKeys = song.artists.map(getKey).sort().join(",");
            return formKeys !== songKeys;
        }
        case "genres": {
            const getKey = (g: {id: number; name: string}) => g.id > 0 ? `id:${g.id}` : `name:${g.name}`;
            const formKeys = form.genres.map(getKey).sort().join(",");
            const songKeys = song.genres.map(getKey).sort().join(",");
            return formKeys !== songKeys;
        }
        default:
            return false;
    }
}

export function shouldSaveField(
    form: FormState,
    song: GetSongResponseSong,
    metadata: SongMetadataDiff | null,
    checkbox: boolean,
    field: keyof FieldCheckboxes
): boolean {
    if (hasFieldMetadataDiff(metadata, field)) {
        return checkbox;
    }
    return isFieldDifferentFromSong(form, song, field as keyof FormState);
}

/**
 * The album and its album artist are saved together, since the album is looked up among the albums of that artist.
 * A half that is not being saved keeps the song's current value.
 */
export function buildAlbumUpdate(
    form: FormState,
    song: GetSongResponseSong,
    metadata: SongMetadataDiff | null,
    checkboxes: FieldCheckboxes
): ValueUpdateOfAlbumRef | undefined {
    const saveAlbum = shouldSaveField(form, song, metadata, checkboxes.album, "album");
    const saveAlbumArtist = shouldSaveField(form, song, metadata, checkboxes.albumArtist, "albumArtist");

    if (!saveAlbum && !saveAlbumArtist) {
        return undefined;
    }

    const albumArtist = saveAlbumArtist ? form.albumArtist : song.album?.artist;

    return {
        newValue: {
            name: saveAlbum ? form.album : song.album?.name ?? "",
            artist: albumArtist?.name
                ? (albumArtist.id > 0 ? { id: albumArtist.id } : { name: albumArtist.name })
                : null,
        },
    };
}

/**
 * The name of the form's album artist when it is not one of the form's artists, which the server rejects.
 */
export function getAlbumArtistMismatch(form: FormState): string | null {
    const albumArtist = form.albumArtist;

    if (!albumArtist?.name || form.artists.length === 0) {
        return null;
    }

    // Typed (not picked) artists have no id yet, and are matched by name on save
    const isMatch = form.artists.some(artist =>
        artist.id > 0 && albumArtist.id > 0 ? artist.id === albumArtist.id : artist.name === albumArtist.name
    );

    return isMatch ? null : albumArtist.name;
}

export function hasChangesToSave(
    form: FormState,
    song: GetSongResponseSong,
    metadata: SongMetadataDiff | null,
    checkboxes: FieldCheckboxes
): boolean {
    const fields: (keyof FieldCheckboxes)[] = [
        "title", "year", "lyrics", "rating", "explicit", "cover",
        "album", "albumArtist", "artists", "genres"
    ];
    return fields.some(field =>
        shouldSaveField(form, song, metadata, checkboxes[field], field)
    );
}