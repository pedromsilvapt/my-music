import { describe, expect, it } from "vitest";
import type { GetSongResponseSong } from "../../model/getSongResponseSong";
import {
    buildAlbumUpdate,
    createInitialCheckboxes,
    formStateFromSong,
    getAlbumArtistMismatch,
} from "./song-edit-types";

const song = {
    id: 1,
    title: "Song",
    isExplicit: false,
    album: { id: 10, name: "Album A", artist: { id: 100, name: "Artist A" } },
    artists: [{ id: 100, name: "Artist A" }],
    genres: [],
} as unknown as GetSongResponseSong;

const checkboxes = createInitialCheckboxes();

describe("buildAlbumUpdate", () => {
    it("sends nothing when neither the album nor the album artist changed", () => {
        expect(buildAlbumUpdate(formStateFromSong(song), song, null, checkboxes)).toBeUndefined();
    });

    it("sends the album name along with the current album artist when only the name changed", () => {
        const form = { ...formStateFromSong(song), album: "Album B" };

        expect(buildAlbumUpdate(form, song, null, checkboxes)).toEqual({
            newValue: { name: "Album B", artist: { id: 100 } },
        });
    });

    it("sends the current album name when only the album artist changed", () => {
        const form = { ...formStateFromSong(song), albumArtist: { id: 200, name: "Artist B" } };

        expect(buildAlbumUpdate(form, song, null, checkboxes)).toEqual({
            newValue: { name: "Album A", artist: { id: 200 } },
        });
    });

    it("sends a typed album artist by name", () => {
        const form = { ...formStateFromSong(song), albumArtist: { id: -1, name: "New Artist" } };

        expect(buildAlbumUpdate(form, song, null, checkboxes)).toEqual({
            newValue: { name: "Album A", artist: { name: "New Artist" } },
        });
    });

    it("sends an empty name when the album is cleared", () => {
        const form = { ...formStateFromSong(song), album: "" };

        expect(buildAlbumUpdate(form, song, null, checkboxes)?.newValue?.name).toBe("");
    });
});

describe("getAlbumArtistMismatch", () => {
    it("matches picked artists by id", () => {
        const form = { ...formStateFromSong(song), albumArtist: { id: 200, name: "Artist A" } };

        expect(getAlbumArtistMismatch(form)).toBe("Artist A");
        expect(getAlbumArtistMismatch(formStateFromSong(song))).toBeNull();
    });

    it("matches a typed artist by name", () => {
        const form = {
            ...formStateFromSong(song),
            artists: [{ id: -1, name: "Artist B" }],
            albumArtist: { id: 200, name: "Artist B" },
        };

        expect(getAlbumArtistMismatch(form)).toBeNull();
    });

    it("reports an album artist that is not one of the artists", () => {
        const form = { ...formStateFromSong(song), artists: [{ id: 200, name: "Artist B" }] };

        expect(getAlbumArtistMismatch(form)).toBe("Artist A");
    });
});
