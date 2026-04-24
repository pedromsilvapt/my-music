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

    const setIsPlaying = async (playing: boolean) => {
        setStoreIsPlaying(playing);
        if (wavesurferRef.current) {
            if (playing) {
                try {
                    await wavesurferRef.current.play();
                } catch (err) {
                    if (err instanceof DOMException && err.name === 'NotAllowedError') {
                        console.warn('[PlayerControlsContainer] play() blocked - user interaction required');
                        notifications.show({
                            title: 'Playback blocked',
                            message: 'Browser requires user interaction to play audio',
                            color: 'yellow',
                            autoClose: 4000,
                        });
                    } else {
                        console.error('[PlayerControlsContainer] play() error:', err);
                    }
                }
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
