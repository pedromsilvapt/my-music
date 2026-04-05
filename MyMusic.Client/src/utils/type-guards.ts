import type {GetPlaylistSongItem, ListSongItem} from "../model";

export function isGetPlaylistSong(song: ListSongItem): song is GetPlaylistSongItem {
    return 'order' in song && typeof (song as GetPlaylistSongItem).order === 'number';
}