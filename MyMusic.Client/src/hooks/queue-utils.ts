import type { GetPlaylistSongItem, ListSongItem } from '../model';
import { isPlaylistSong } from '../utils/type-guards';

export type PlayableItem = GetPlaylistSongItem | ListSongItem;

export { isPlaylistSong };

export function toPlaylistSong (song: PlayableItem, order: number): GetPlaylistSongItem {
    if (isPlaylistSong(song)) {
        return { ...song, order };
    }
    return {
        ...song,
        order,
        addedAtPlaylist: new Date().toISOString(),
        stopAfterPlayback: false,
        skipNextPlayback: false,
    } as GetPlaylistSongItem;
}

export function compactOrders (songs: GetPlaylistSongItem[]): GetPlaylistSongItem[] {
    return songs.map((song, index) => ({
        ...song,
        order: index + 1,
    }));
}

export function insertAfterCurrent (
    songs: GetPlaylistSongItem[],
    newSongs: GetPlaylistSongItem[],
    currentSongId: number | null | undefined
): GetPlaylistSongItem[] {
    if (!currentSongId || songs.length === 0) {
        return compactOrders([...newSongs, ...songs]);
    }

    const currentIndex = songs.findIndex((s) => s.id === currentSongId);
    if (currentIndex < 0) {
        return compactOrders([...newSongs, ...songs]);
    }

    const beforeCurrent = songs.slice(0, currentIndex + 1);
    const afterCurrent = songs.slice(currentIndex + 1);
    return compactOrders([...beforeCurrent, ...newSongs, ...afterCurrent]);
}

export function appendToEnd (songs: GetPlaylistSongItem[], newSongs: GetPlaylistSongItem[]): GetPlaylistSongItem[] {
    return compactOrders([...songs, ...newSongs]);
}

export function filterOutSongIds (songs: GetPlaylistSongItem[], songIdsToRemove: Set<number>): GetPlaylistSongItem[] {
    return compactOrders(songs.filter((s) => !songIdsToRemove.has(s.id)));
}

export function playNextSongs (
    queue: GetPlaylistSongItem[],
    songs: PlayableItem[],
    currentSongId: number | null | undefined
): GetPlaylistSongItem[] {
    const songIds = songs.map((s) => s.id);
    const filteredQueue = filterOutSongIds(queue, new Set(songIds));
    const newSongs = songs.map((song, i) => toPlaylistSong(song, i + 1));
    return insertAfterCurrent(filteredQueue, newSongs, currentSongId);
}

export function playLastSongs (
    queue: GetPlaylistSongItem[],
    songs: PlayableItem[]
): GetPlaylistSongItem[] {
    const songIds = songs.map((s) => s.id);
    const filteredQueue = filterOutSongIds(queue, new Set(songIds));
    const newSongs = songs.map((song, i) => toPlaylistSong(song, filteredQueue.length + i + 1));
    return appendToEnd(filteredQueue, newSongs);
}

export function reorderSongs (
    songs: GetPlaylistSongItem[],
    fromIndex: number,
    toIndex: number
): GetPlaylistSongItem[] {
    if (fromIndex < 0 || fromIndex >= songs.length || toIndex < 0 || toIndex >= songs.length) {
        return songs;
    }

    const result = [...songs];
    const [movedSong] = result.splice(fromIndex, 1);
    result.splice(toIndex, 0, movedSong);
    return compactOrders(result);
}