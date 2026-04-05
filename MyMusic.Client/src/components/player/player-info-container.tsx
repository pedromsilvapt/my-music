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
        <PlayerInfo song={song} setIsFavorite={setIsFavorite} />
    );
}
