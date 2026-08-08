import {useRef} from 'react';
import {useShallow} from 'zustand/react/shallow';
import {notifications} from '@mantine/notifications';
import {useTranslation} from 'react-i18next';
import {useMediaSession} from "../../hooks/use-media-session";
import {usePlayHistoryTracker} from "../../hooks/use-play-history.ts";
import {usePlayerNavigation} from '../../hooks/use-player-navigation';
import {useQueue} from '../../hooks/use-queue';
import {usePlaybackActions, usePlaybackStore} from '../../stores/playback-store';
import PlayerTimeline from './player-timeline';
import {useWavesurferRef} from './wavesurfer-context';
import {useBatchSetStopAfterPlayback} from '../../client/playlists';
import type {GetPlaylistSongItem} from '../../model';

const TIME_UPDATE_DEBOUNCE_INTERVAL_SECONDS = .25;
const MAX_LOAD_RETRIES = 3;

export default function PlayerTimelineContainer() {
    const {t} = useTranslation(["player", "queue", "common"]);
    usePlayHistoryTracker();
    useMediaSession();

    const previousIntervalRef = useRef<number>(-1);
    const loadRetryCountRef = useRef<number>(0);
    const lastSongIdRef = useRef<number | null>(null);
    const wavesurferRef = useWavesurferRef();
    const {goForward, hasNext} = usePlayerNavigation();
    const {queue, currentSongId, queueId} = useQueue();
    const {setIsPlaying, setCurrentTime, load, incrementPlaybackKey} = usePlaybackActions((s) => ({
        setIsPlaying: s.setIsPlaying,
        setCurrentTime: s.setCurrentTime,
        load: s.load,
        incrementPlaybackKey: s.incrementPlaybackKey,
    }));
    const clearStopAfterPlaybackRef = useRef(useBatchSetStopAfterPlayback({}));

    const {song, time, duration, songUrl, autoplay, volume, muted, playbackKey} = usePlaybackStore(
        useShallow((s) => {
            if (s.current.type === 'LOADED') {
                return {
                    song: s.current.song,
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
                    song: s.current.song,
                    time: 0,
                    duration: 0,
                    songUrl: `/api/songs/${s.current.song.id}/download`,
                    autoplay: s.autoplay,
                    volume: s.output.volume,
                    muted: s.output.muted,
                    playbackKey: s.playbackKey,
                };
            }
            return {song: null, time: 0, duration: 0, songUrl: null, autoplay: false, volume: 1, muted: false, playbackKey: 0};
        })
    );

    if (song && lastSongIdRef.current !== song.id) {
        lastSongIdRef.current = song.id;
        loadRetryCountRef.current = 0;
    }

    const handleTimeUpdate = (time: number) => {
        const currentInterval = Math.floor(time / TIME_UPDATE_DEBOUNCE_INTERVAL_SECONDS);
        if (currentInterval !== previousIntervalRef.current) {
            previousIntervalRef.current = currentInterval;
            setCurrentTime(time);
        }
    };

    const attemptAutoplay = async () => {
        if (!wavesurferRef.current) return;
        try {
            await wavesurferRef.current.play();
        } catch (err) {
            if (err instanceof DOMException && err.name === 'NotAllowedError') {
                console.warn('[PlayerTimelineContainer] Autoplay blocked by browser - user interaction required');
                notifications.show({
                    title: t("player:notifications.autoplayBlockedTitle"),
                    message: t("player:notifications.autoplayBlockedMessage"),
                    color: 'yellow',
                    autoClose: 5000,
                });
            } else {
                console.error('[PlayerTimelineContainer] play() error:', err);
            }
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
                attemptAutoplay();
            }
        }
    };

    const attemptRetryOrSkip = (error: Error, currentSong: GetPlaylistSongItem | null) => {
        const retryCount = loadRetryCountRef.current + 1;
        loadRetryCountRef.current = retryCount;

        console.error(`[PlayerTimelineContainer] Load error (attempt ${retryCount}/${MAX_LOAD_RETRIES}):`, error);

        if (retryCount < MAX_LOAD_RETRIES) {
            notifications.show({
                title: t("player:notifications.loadingFailedTitle"),
                message: t("player:notifications.loadingFailedMessage", {attempt: retryCount + 1, total: MAX_LOAD_RETRIES}),
                color: 'yellow',
                autoClose: 3000,
            });
            incrementPlaybackKey();
        } else {
            notifications.show({
                title: t("player:notifications.songLoadFailedTitle"),
                message: currentSong ? t("player:notifications.songLoadFailedNamed", {title: currentSong.title}) : t("player:notifications.songLoadFailed"),
                color: 'red',
                autoClose: 5000,
            });

            if (hasNext) {
                console.log('[PlayerTimelineContainer] Skipping to next song after failed retries');
                const result = goForward();
                if (result?.allRemainingSkipped) {
                    wavesurferRef.current?.stop();
                    setIsPlaying(false);
                    notifications.show({
                        title: t("player:notifications.playbackStoppedTitle"),
                        message: t("player:notifications.playbackStoppedMessage"),
                        autoClose: 4000,
                    });
                }
            } else {
                console.log('[PlayerTimelineContainer] No more songs to play after failed load');
                wavesurferRef.current?.stop();
                setIsPlaying(false);
            }
        }
    };

    const handleError = (error: Error) => {
        console.error('[PlayerTimelineContainer] WaveSurfer error:', error, 'song:', song?.id, song?.title);
        attemptRetryOrSkip(error, song);
    };

    const handleFinish = () => {
        const currentSong = queue.find(s => s.id === currentSongId);
        if (currentSong?.stopAfterPlayback) {
            setIsPlaying(false);
            notifications.show({
                title: t("player:notifications.stoppedAfterSongTitle"),
                message: t("player:notifications.stoppedAfterSongMessage", {title: currentSong.title}),
                autoClose: 4000,
            });
            if (queueId != null && currentSongId != null) {
                clearStopAfterPlaybackRef.current.mutate(
                    {
                        data: {
                            playlistId: queueId,
                            songIds: [currentSongId],
                            stopAfterPlayback: false,
                        },
                    },
                    {
                        onError: () => {
                            notifications.show({
                                title: t("common:status.error"),
                                message: t("player:notifications.stopAfterClearFailedMessage"),
                                color: 'red',
                                autoClose: 4000,
                            });
                        },
                    }
                );
            }
            return;
        }

        const result = goForward();

        if (result?.allRemainingSkipped) {
            wavesurferRef.current?.stop();
            setIsPlaying(false);
            notifications.show({
                title: t("player:notifications.playbackStoppedTitle"),
                message: t("player:notifications.playbackStoppedMessage"),
                autoClose: 4000,
            });
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
            onFinish={handleFinish}
            onError={handleError}
        />
    );
}
