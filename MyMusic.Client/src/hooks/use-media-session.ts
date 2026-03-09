import {useEffect, useRef, useState} from 'react';
import {useShallow} from 'zustand/react/shallow';
import {useWavesurferRef} from '../components/player/wavesurfer-context';
import type {GetPlaylistSong} from '../model';
import {usePlaybackActions, usePlaybackStore} from '../stores/playback-store';
import {usePlayerNavigation} from './use-player-navigation';

const SEEK_TIME_SECONDS = 10;

type ArtworkMetadata = {
    id: number;
    mimeType: string;
    width: number;
    height: number;
};

export function useMediaSession() {
    const wavesurferRef = useWavesurferRef();
    const navigation = usePlayerNavigation();
    const {setIsPlaying: setStoreIsPlaying, setCurrentTime} = usePlaybackActions((s) => ({
        setIsPlaying: s.setIsPlaying,
        setCurrentTime: s.setCurrentTime,
    }));

    const {song, isPlaying, time, duration} = usePlaybackStore(
        useShallow((s) => {
            if (s.current.type === 'LOADED') {
                return {
                    song: s.current.song,
                    isPlaying: s.current.isPlaying,
                    time: s.current.time,
                    duration: s.current.duration,
                };
            }
            if (s.current.type === 'LOADING') {
                return {
                    song: s.current.song,
                    isPlaying: false,
                    time: 0,
                    duration: 0,
                };
            }
            return {song: null, isPlaying: false, time: 0, duration: 0};
        })
    );

    const [artworkMetadata, setArtworkMetadata] = useState<ArtworkMetadata | null>(null);
    const songRef = useRef<GetPlaylistSong | null>(null);
    const isPlayingRef = useRef<boolean>(false);
    const timeRef = useRef<number>(0);
    const durationRef = useRef<number>(0);
    const navigationRef = useRef(navigation);
    const setStoreIsPlayingRef = useRef(setStoreIsPlaying);
    const setCurrentTimeRef = useRef(setCurrentTime);

    useEffect(() => {
        songRef.current = song;
        isPlayingRef.current = isPlaying;
        timeRef.current = time;
        durationRef.current = duration;
    }, [song, isPlaying, time, duration]);

    useEffect(() => {
        navigationRef.current = navigation;
    }, [navigation]);

    useEffect(() => {
        setStoreIsPlayingRef.current = setStoreIsPlaying;
    }, [setStoreIsPlaying]);

    useEffect(() => {
        setCurrentTimeRef.current = setCurrentTime;
    }, [setCurrentTime]);

    useEffect(() => {
        if (!song?.cover) {
            setArtworkMetadata(null);
            return;
        }

        let cancelled = false;
        const coverId = song.cover;

        fetch(`/api/artwork/${coverId}/metadata`)
            .then((res) => {
                if (!res.ok) throw new Error('Failed to fetch metadata');
                return res.json();
            })
            .then((data: ArtworkMetadata) => {
                if (!cancelled) {
                    setArtworkMetadata(data);
                }
            })
            .catch(() => {
                if (!cancelled) {
                    setArtworkMetadata(null);
                }
            });

        return () => {
            cancelled = true;
        };
    }, [song?.cover]);

    const isActive = song !== null;

    useEffect(() => {
        if (!('mediaSession' in navigator)) {
            return;
        }

        if (!isActive) {
            navigator.mediaSession.setActionHandler('play', null);
            navigator.mediaSession.setActionHandler('pause', null);
            navigator.mediaSession.setActionHandler('previoustrack', null);
            navigator.mediaSession.setActionHandler('nexttrack', null);
            navigator.mediaSession.setActionHandler('seekforward', null);
            navigator.mediaSession.setActionHandler('seekbackward', null);
            navigator.mediaSession.setActionHandler('seekto', null);
            return;
        }

        const handlePlay = () => {
            setStoreIsPlayingRef.current(true);
            wavesurferRef.current?.play();
        };

        const handlePause = () => {
            setStoreIsPlayingRef.current(false);
            wavesurferRef.current?.pause();
        };

        const handlePreviousTrack = () => {
            if (navigationRef.current.hasPrevious) {
                navigationRef.current.goBackward();
            }
        };

        const handleNextTrack = () => {
            if (navigationRef.current.hasNext) {
                navigationRef.current.goForward();
            }
        };

        const handleSeekForward = (details: MediaSessionActionDetails) => {
            const seekTime = details.seekOffset ?? SEEK_TIME_SECONDS;
            const newTime = Math.min(timeRef.current + seekTime, durationRef.current);
            if (durationRef.current > 0) {
                wavesurferRef.current?.seekTo(newTime / durationRef.current);
                setCurrentTimeRef.current(newTime);
            }
        };

        const handleSeekBackward = (details: MediaSessionActionDetails) => {
            const seekTime = details.seekOffset ?? SEEK_TIME_SECONDS;
            const newTime = Math.max(timeRef.current - seekTime, 0);
            if (durationRef.current > 0) {
                wavesurferRef.current?.seekTo(newTime / durationRef.current);
                setCurrentTimeRef.current(newTime);
            }
        };

        const handleSeekTo = (details: MediaSessionActionDetails) => {
            if (details.seekTime !== undefined && durationRef.current > 0) {
                const newTime = Math.max(0, Math.min(details.seekTime, durationRef.current));
                wavesurferRef.current?.seekTo(newTime / durationRef.current);
                setCurrentTimeRef.current(newTime);
            }
        };

        navigator.mediaSession.setActionHandler('play', handlePlay);
        navigator.mediaSession.setActionHandler('pause', handlePause);
        navigator.mediaSession.setActionHandler('previoustrack', handlePreviousTrack);
        navigator.mediaSession.setActionHandler('nexttrack', handleNextTrack);
        navigator.mediaSession.setActionHandler('seekforward', handleSeekForward);
        navigator.mediaSession.setActionHandler('seekbackward', handleSeekBackward);
        navigator.mediaSession.setActionHandler('seekto', handleSeekTo);
    }, [wavesurferRef, isActive]);

    useEffect(() => {
        if (!('mediaSession' in navigator) || !song) {
            return;
        }

        const artists = song.artists.map((a) => a.name).join(', ');
        const album = song.album.name;
        const title = song.title;

        const artwork: MediaImage[] = [];
        if (song.cover) {
            const maxSize = artworkMetadata
                ? Math.max(artworkMetadata.width, artworkMetadata.height)
                : 512;
            const mimeType = artworkMetadata?.mimeType ?? 'image/jpeg';

            if (maxSize >= 512) {
                artwork.push({
                    src: `/api/artwork/${song.cover}?size=512`,
                    sizes: '512x512',
                    type: mimeType,
                });
            }
            if (maxSize >= 256) {
                artwork.push({
                    src: `/api/artwork/${song.cover}?size=256`,
                    sizes: '256x256',
                    type: mimeType,
                });
            }
            if (maxSize >= 128) {
                artwork.push({
                    src: `/api/artwork/${song.cover}?size=128`,
                    sizes: '128x128',
                    type: mimeType,
                });
            }

            if (artwork.length === 0) {
                artwork.push({
                    src: `/api/artwork/${song.cover}`,
                    sizes: `${maxSize}x${maxSize}`,
                    type: mimeType,
                });
            }
        }

        navigator.mediaSession.metadata = new MediaMetadata({
            title,
            artist: artists,
            album,
            artwork,
        });
    }, [song, artworkMetadata]);

    useEffect(() => {
        if (!('mediaSession' in navigator)) {
            return;
        }

        navigator.mediaSession.playbackState = isPlaying ? 'playing' : 'paused';
    }, [isPlaying]);

    useEffect(() => {
        if (!('mediaSession' in navigator) || duration <= 0) {
            return;
        }

        if ('setPositionState' in navigator.mediaSession) {
            navigator.mediaSession.setPositionState({
                duration,
                position: time,
                playbackRate: 1,
            });
        }
    }, [time, duration]);
}