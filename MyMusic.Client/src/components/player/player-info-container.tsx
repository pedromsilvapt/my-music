import {usePlaybackActions, usePlaybackStore} from '../../stores/playback-store';
import PlayerInfo from './player-info';

export default function PlayerInfoContainer() {
    const song = usePlaybackStore((s) => {
        if (s.current.type === 'LOADED' || s.current.type === 'LOADING') {
            return s.current.song;
        }
        return null;
    });
    const {setIsFavorite} = usePlaybackActions((s) => ({setIsFavorite: s.setIsFavorite}));

    if (!song) return null;

    return (
        <PlayerInfo
            artwork={song.cover}
            title={song.title}
            album={song.album.name}
            artists={song.artists.map(a => a.name)}
            year={song.year}
            isExplicit={song.isExplicit}
            isFavorite={song.isFavorite}
            setIsFavorite={setIsFavorite}
            id={song.id}
        />
    );
}
