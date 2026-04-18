import type {GetPlaylistSongItem, ListSongItem} from '../model';

export function isPlaylistSong(song: ListSongItem): song is GetPlaylistSongItem {
    return 'order' in song && typeof (song as GetPlaylistSongItem).order === 'number';
}