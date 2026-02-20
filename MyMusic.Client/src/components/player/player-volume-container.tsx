import {useEffect} from 'react';
import {usePlaybackActions, usePlaybackStore} from '../../stores/playback-store';
import PlayerVolume from './player-volume';
import {useWavesurferRef} from './wavesurfer-context';

export default function PlayerVolumeContainer() {
    const wavesurferRef = useWavesurferRef();
    const volume = usePlaybackStore((s) => s.output.volume);
    const muted = usePlaybackStore((s) => s.output.muted);
    const {setVolume, setMuted} = usePlaybackActions((s) => ({
        setVolume: s.setVolume,
        setMuted: s.setMuted,
    }));

    useEffect(() => {
        if (wavesurferRef.current) {
            wavesurferRef.current.setVolume(volume);
        }
    }, [wavesurferRef, volume]);

    useEffect(() => {
        if (wavesurferRef.current) {
            wavesurferRef.current.setMuted(muted);
        }
    }, [wavesurferRef, muted]);

    return (
        <PlayerVolume
            volume={volume}
            setVolume={setVolume}
            isMuted={muted}
            setIsMuted={setMuted}
        />
    );
}
