import { describe, expect, it } from 'vitest';
import {
    appendToEnd,
    compactOrders,
    filterOutSongIds,
    insertAfterCurrent,
    isPlaylistSong,
    playLastSongs,
    playNextSongs,
} from './queue-utils';
import type { GetPlaylistSongItem, ListSongItem } from '../model';

function makePlaylistSong (id: number, order: number, stopAfterPlayback = false): GetPlaylistSongItem {
    return {
        id,
        order,
        addedAtPlaylist: '2025-01-01T00:00:00Z',
        stopAfterPlayback,
        skipNextPlayback: false,
        cover: null,
        title: `Song ${id}`,
        artists: [],
        album: { id: 1, name: 'Album' },
        genres: [],
        year: null,
        duration: '3:00',
        devices: [],
        isFavorite: false,
        isExplicit: false,
        hasLyrics: false,
        createdAt: '2025-01-01T00:00:00Z',
    };
}

function makeListSong (id: number): ListSongItem {
    return {
        id,
        cover: null,
        title: `Song ${id}`,
        artists: [],
        album: { id: 1, name: 'Album' },
        genres: [],
        year: null,
        duration: '3:00',
        devices: [],
        isFavorite: false,
        isExplicit: false,
        hasLyrics: false,
        createdAt: '2025-01-01T00:00:00Z',
    };
}

describe('filterOutSongIds', () => {
    it('removes songs matching the given IDs and re-compacts orders', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
            makePlaylistSong(3, 3),
            makePlaylistSong(4, 4),
            makePlaylistSong(5, 5),
        ];

        const result = filterOutSongIds(queue, new Set([2, 4]));

        expect(result).toHaveLength(3);
        expect(result.map((s) => s.id)).toEqual([1, 3, 5]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3]);
    });

    it('returns the same queue when no IDs match', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
        ];

        const result = filterOutSongIds(queue, new Set([99]));

        expect(result).toHaveLength(2);
        expect(result.map((s) => s.id)).toEqual([1, 2]);
    });

    it('returns empty array when all IDs are removed', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
        ];

        const result = filterOutSongIds(queue, new Set([1, 2]));

        expect(result).toHaveLength(0);
    });

    it('handles empty queue', () => {
        const result = filterOutSongIds([], new Set([1, 2]));

        expect(result).toHaveLength(0);
    });
});

describe('insertAfterCurrent', () => {
    it('inserts new songs after the current song', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
            makePlaylistSong(3, 3),
        ];
        const newSongs = [
            makePlaylistSong(4, 1),
            makePlaylistSong(5, 2),
        ];

        const result = insertAfterCurrent(queue, newSongs, 2);

        expect(result.map((s) => s.id)).toEqual([1, 2, 4, 5, 3]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4, 5]);
    });

    it('inserts at the beginning when no current song', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
        ];
        const newSongs = [makePlaylistSong(3, 1)];

        const result = insertAfterCurrent(queue, newSongs, null);

        expect(result.map((s) => s.id)).toEqual([3, 1, 2]);
    });

    it('inserts at the beginning when current song ID not found', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
        ];
        const newSongs = [makePlaylistSong(3, 1)];

        const result = insertAfterCurrent(queue, newSongs, 99);

        expect(result.map((s) => s.id)).toEqual([3, 1, 2]);
    });
});

describe('appendToEnd', () => {
    it('appends new songs to the end of the queue', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
        ];
        const newSongs = [
            makePlaylistSong(3, 3),
            makePlaylistSong(4, 4),
        ];

        const result = appendToEnd(queue, newSongs);

        expect(result.map((s) => s.id)).toEqual([1, 2, 3, 4]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4]);
    });
});

describe('playNextSongs', () => {
    it('moves existing songs after current song instead of duplicating them', () => {
        // Queue: [A, B, C, D, E], current song: B, Play Next: [D, E]
        // Expected: [A, B, D, E, C] (D and E moved after B)
        const queue = [
            makePlaylistSong(1, 1), // A
            makePlaylistSong(2, 2), // B (current)
            makePlaylistSong(3, 3), // C
            makePlaylistSong(4, 4), // D
            makePlaylistSong(5, 5), // E
        ];
        const currentSongId = 2; // B
        const songsToAdd: GetPlaylistSongItem[] = [queue[3], queue[4]]; // D, E

        const result = playNextSongs(queue, songsToAdd, currentSongId);

        expect(result.map((s) => s.id)).toEqual([1, 2, 4, 5, 3]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4, 5]);
        // Verify no duplicates: all IDs should be unique
        expect(new Set(result.map((s) => s.id)).size).toBe(result.length);
    });

    it('handles mix of new and existing songs with Play Next', () => {
        // Queue: [A, B, C, D], current song: B, Play Next: [D, F]
        // D is existing, F is new
        // Expected: [A, B, D, F, C]
        const queue = [
            makePlaylistSong(1, 1), // A
            makePlaylistSong(2, 2), // B (current)
            makePlaylistSong(3, 3), // C
            makePlaylistSong(4, 4), // D
        ];
        const currentSongId = 2; // B
        // D is in queue, F is not
        const playableItems: ListSongItem[] = [makeListSong(4), makeListSong(6)];

        const result = playNextSongs(queue, playableItems, currentSongId);

        expect(result.map((s) => s.id)).toEqual([1, 2, 4, 6, 3]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4, 5]);
        expect(new Set(result.map((s) => s.id)).size).toBe(result.length);
    });
});

describe('playLastSongs', () => {
    it('moves existing songs to end instead of duplicating them', () => {
        // Queue: [A, B, C, D, E], Play Last: [D, E]
        // Expected: [A, B, C, D, E] (D and E moved to end)
        const queue = [
            makePlaylistSong(1, 1), // A
            makePlaylistSong(2, 2), // B
            makePlaylistSong(3, 3), // C
            makePlaylistSong(4, 4), // D
            makePlaylistSong(5, 5), // E
        ];
        const songsToAdd: GetPlaylistSongItem[] = [queue[3], queue[4]]; // D, E

        const result = playLastSongs(queue, songsToAdd);

        expect(result.map((s) => s.id)).toEqual([1, 2, 3, 4, 5]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4, 5]);
        expect(new Set(result.map((s) => s.id)).size).toBe(result.length);
    });

    it('handles mix of new and existing songs with Play Last', () => {
        // Queue: [A, B, C, D], Play Last: [D, F]
        // D is existing, F is new
        // Expected: [A, B, C, D, F]
        const queue = [
            makePlaylistSong(1, 1), // A
            makePlaylistSong(2, 2), // B
            makePlaylistSong(3, 3), // C
            makePlaylistSong(4, 4), // D
        ];
        const playableItems: ListSongItem[] = [makeListSong(4), makeListSong(6)];

        const result = playLastSongs(queue, playableItems);

        expect(result.map((s) => s.id)).toEqual([1, 2, 3, 4, 6]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4, 5]);
        expect(new Set(result.map((s) => s.id)).size).toBe(result.length);
    });

    it('appends only new songs when none exist in the queue', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
        ];
        const playableItems: ListSongItem[] = [makeListSong(3), makeListSong(4)];

        const result = playLastSongs(queue, playableItems);

        expect(result.map((s) => s.id)).toEqual([1, 2, 3, 4]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4]);
    });

    it('moves existing songs from the middle to the end', () => {
        // Queue: [A, B, C, D, E], Play Last: [B, C]
        // Expected: [A, D, E, B, C] (B and C removed from middle, appended to end)
        const queue = [
            makePlaylistSong(1, 1), // A
            makePlaylistSong(2, 2), // B
            makePlaylistSong(3, 3), // C
            makePlaylistSong(4, 4), // D
            makePlaylistSong(5, 5), // E
        ];
        const songsToAdd: GetPlaylistSongItem[] = [queue[1], queue[2]]; // B, C

        const result = playLastSongs(queue, songsToAdd);

        expect(result.map((s) => s.id)).toEqual([1, 4, 5, 2, 3]);
        expect(result.map((s) => s.order)).toEqual([1, 2, 3, 4, 5]);
        expect(new Set(result.map((s) => s.id)).size).toBe(result.length);
    });
});

describe('compactOrders', () => {
    it('re-numbers orders sequentially starting from 1', () => {
        const songs = [
            makePlaylistSong(1, 5),
            makePlaylistSong(2, 10),
            makePlaylistSong(3, 99),
        ];

        const result = compactOrders(songs);

        expect(result.map((s) => s.order)).toEqual([1, 2, 3]);
    });
});

describe('order to array index conversion', () => {
    it('converts 1-indexed order to 0-indexed array position', () => {
        const queue = [
            makePlaylistSong(1, 1),
            makePlaylistSong(2, 2),
            makePlaylistSong(3, 3),
        ];

        // When navigating to a song with order N, use index N-1
        // This is critical for goTo() and similar functions expecting array indices
        const songWithOrder1 = queue[0];
        const songWithOrder2 = queue[1];
        const songWithOrder3 = queue[2];

        expect(songWithOrder1.order).toBe(1);
        expect(songWithOrder2.order).toBe(2);
        expect(songWithOrder3.order).toBe(3);

        // Verify: order N corresponds to array index N-1
        expect(queue[songWithOrder1.order - 1]).toBe(songWithOrder1);
        expect(queue[songWithOrder2.order - 1]).toBe(songWithOrder2);
        expect(queue[songWithOrder3.order - 1]).toBe(songWithOrder3);

        // Common bug: using order directly as index plays the wrong song
        // queue[songWithOrder1.order] would give songWithOrder2 (wrong!)
        expect(queue[songWithOrder1.order]).toBe(songWithOrder2);
    });
});

describe('isPlaylistSong', () => {
    it('returns true for a playlist song with order property', () => {
        const playlistSong = makePlaylistSong(1, 1);

        expect(isPlaylistSong(playlistSong)).toBe(true);
    });

    it('returns false for a plain list song without order property', () => {
        const listSong = makeListSong(1);

        expect(isPlaylistSong(listSong)).toBe(false);
    });

    it('enables access to stopAfterPlayback on playlist songs', () => {
        const song = makePlaylistSong(1, 1, true);

        if (isPlaylistSong(song)) {
            expect(song.stopAfterPlayback).toBe(true);
        }
    });

    it('returns false for list song even if stopAfterPlayback key is checked', () => {
        const listSong = makeListSong(1);

        expect(isPlaylistSong(listSong)).toBe(false);
        expect('stopAfterPlayback' in listSong).toBe(false);
    });
});