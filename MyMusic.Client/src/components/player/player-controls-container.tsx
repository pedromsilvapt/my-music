import {usePlayerNavigation} from '../../hooks/use-player-navigation';
import {usePlaybackActions, usePlaybackStore} from '../../stores/playback-store';
import PlayerControls from './player-controls';
import {useWavesurferRef} from './wavesurfer-context';

export default function PlayerControlsContainer() {
    const wavesurferRef = useWavesurferRef();
    const isPlaying = usePlaybackStore((s) =>
        s.current.type === 'LOADED' ? s.current.isPlaying : false
    );
    const {setIsPlaying: setStoreIsPlaying} = usePlaybackActions((s) => ({
        setIsPlaying: s.setIsPlaying,
    }));
    const {goForward, goBackward, hasNext, hasPrevious} = usePlayerNavigation();

    const setIsPlaying = (playing: boolean) => {
        setStoreIsPlaying(playing);
        if (wavesurferRef.current) {
            if (playing) {
                wavesurferRef.current.play();
            } else {
                wavesurferRef.current.pause();
            }
        }
    };

    return (
        <PlayerControls
            isPlaying={isPlaying}
            setIsPlaying={setIsPlaying}
            hasNext={hasNext}
            hasPrevious={hasPrevious}
            playPrevious={goBackward}
            playNext={goForward}
        />
    );
}
