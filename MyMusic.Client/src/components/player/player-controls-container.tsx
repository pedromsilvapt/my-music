import {notifications} from '@mantine/notifications';
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

    const handlePlayNext = () => {
        const result = goForward();
        if (result?.allRemainingSkipped) {
            setIsPlaying(false);
            notifications.show({
                title: 'Playback stopped',
                message: 'All remaining songs in the queue are flagged to skip.',
                autoClose: 4000,
            });
        }
    };

    return (
        <PlayerControls
            isPlaying={isPlaying}
            setIsPlaying={setIsPlaying}
            hasNext={hasNext}
            hasPrevious={hasPrevious}
            playPrevious={goBackward}
            playNext={handlePlayNext}
        />
    );
}
