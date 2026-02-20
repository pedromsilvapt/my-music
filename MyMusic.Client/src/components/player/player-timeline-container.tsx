import {useRef} from 'react';
import {useShallow} from 'zustand/react/shallow';
import {usePlayerNavigation} from '../../hooks/use-player-navigation';
import {usePlaybackActions, usePlaybackStore} from '../../stores/playback-store';
import PlayerTimeline from './player-timeline';
import {useWavesurferRef} from './wavesurfer-context';

const TIME_UPDATE_DEBOUNCE_INTERVAL_SECONDS = .25;

export default function PlayerTimelineContainer() {
    const previousIntervalRef = useRef<number>(-1);
    const wavesurferRef = useWavesurferRef();
    const {goForward} = usePlayerNavigation();
    const {setIsPlaying, setCurrentTime, load} = usePlaybackActions((s) => ({
        setIsPlaying: s.setIsPlaying,
        setCurrentTime: s.setCurrentTime,
        load: s.load,
    }));

    const {time, duration, songUrl, autoplay, volume, muted, playbackKey} = usePlaybackStore(
        useShallow((s) => {
            if (s.current.type === 'LOADED') {
                return {
                    time: s.current.time,
                    duration: s.current.duration,
                    songUrl: `/api/songs/${s.current.song.id}/download`,
                    autoplay: s.autoplay,
                    volume: s.output.volume,
                    muted: s.output.muted,
                    playbackKey: s.playbackKey,
                };
            }
            if (s.current.type === 'LOADING') {
                return {
                    time: 0,
                    duration: 0,
                    songUrl: `/api/songs/${s.current.song.id}/download`,
                    autoplay: s.autoplay,
                    volume: s.output.volume,
                    muted: s.output.muted,
                    playbackKey: s.playbackKey,
                };
            }
            return {time: 0, duration: 0, songUrl: null, autoplay: false, volume: 1, muted: false, playbackKey: 0};
        })
    );

    const handleTimeUpdate = (time: number) => {
        const currentInterval = Math.floor(time / TIME_UPDATE_DEBOUNCE_INTERVAL_SECONDS);
        if (currentInterval !== previousIntervalRef.current) {
            previousIntervalRef.current = currentInterval;
            setCurrentTime(time);
        }
    };

    const handleLoad = (wsDuration: number) => {
        load(wsDuration);
        if (wavesurferRef.current) {
            wavesurferRef.current.setVolume(volume);
            wavesurferRef.current.setMuted(muted);
            if (time > 0) {
                wavesurferRef.current.setTime(time);
            }
            if (autoplay) {
                wavesurferRef.current.play();
            }
        }
    };

    if (!songUrl) return null;

    return (
        <PlayerTimeline
            key={playbackKey}
            song={songUrl}
            time={time}
            duration={duration}
            setTime={handleTimeUpdate}
            setIsPlaying={setIsPlaying}
            onLoad={handleLoad}
            onFinish={goForward}
        />
    );
}
