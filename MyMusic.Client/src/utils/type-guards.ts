import type {GetPlaylistSong, ListSongsItem} from "../model";

export function isGetPlaylistSong(song: ListSongsItem): song is GetPlaylistSong {
    return 'order' in song && typeof (song as GetPlaylistSong).order === 'number';
}