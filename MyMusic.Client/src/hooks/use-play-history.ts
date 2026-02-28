import {useEffect, useMemo, useRef, useState} from 'react';
import {useShallow} from 'zustand/react/shallow';
import {useRecordPlayHistory} from '../client/play-history';
import {usePlaybackStore} from '../stores/playback-store';

const SEEK_THRESHOLD_SECONDS = 1;
const PLAY_THRESHOLD_PERCENT = 0.8;

export function usePlayHistoryTracker() {
    const recordPlayMutation = useRecordPlayHistory();
    const mutateRef = useRef(recordPlayMutation.mutate);
    mutateRef.current = recordPlayMutation.mutate;

    const [recordedClientId, setRecordedClientId] = useState<string | null>(null);
    const [accumulatedSeconds, setAccumulatedSeconds] = useState(0);

    const lastTimeRef = useRef(0);
    const lastPlaybackKeyRef = useRef(0);

    const songId = usePlaybackStore(
        useShallow((state) => {
            if (state.current.type === 'LOADED' || state.current.type === 'LOADING') {
                return state.current.song.id;
            }
            return null;
        })
    );

    const playbackKey = usePlaybackStore((state) => state.playbackKey);

    const isPlaying = usePlaybackStore(
        useShallow((state) => {
            if (state.current.type === 'LOADED') {
                return state.current.isPlaying;
            }
            return false;
        })
    );

    const {time, duration} = usePlaybackStore(
        useShallow((state) => {
            if (state.current.type === 'LOADED') {
                return {time: state.current.time, duration: state.current.duration};
            }
            return {time: 0, duration: 0};
        })
    );

    const clientId = useMemo(() => {
        if (songId && playbackKey > 0) {
            return crypto.randomUUID();
        }
        return null;
    }, [playbackKey, songId]);

    const isSeek = useMemo(
        () => Math.abs(time - lastTimeRef.current) > SEEK_THRESHOLD_SECONDS,
        [time]
    );

    const isOverThreshold = useMemo(
        () => duration > 0 && accumulatedSeconds / duration >= PLAY_THRESHOLD_PERCENT,
        [accumulatedSeconds, duration]
    );

    const hasRecorded = useMemo(
        () => clientId !== null && clientId === recordedClientId,
        [clientId, recordedClientId]
    );

    useEffect(() => {
        if (playbackKey !== lastPlaybackKeyRef.current) {
            setAccumulatedSeconds(0);
            lastTimeRef.current = 0;
            lastPlaybackKeyRef.current = playbackKey;
        }
    }, [playbackKey]);

    useEffect(() => {
        if (!isPlaying || isSeek) {
            lastTimeRef.current = time;
            return;
        }

        const delta = time - lastTimeRef.current;
        if (delta > 0) {
            setAccumulatedSeconds((prev) => prev + delta);
        }
        lastTimeRef.current = time;
    }, [time, isPlaying, isSeek]);

    useEffect(() => {
        if (!songId || !isPlaying || hasRecorded || !isOverThreshold || !clientId) {
            return;
        }

        setRecordedClientId(clientId);
        mutateRef.current({
            data: {songId, clientId},
        });
    }, [songId, isPlaying, hasRecorded, isOverThreshold, clientId]);
}
