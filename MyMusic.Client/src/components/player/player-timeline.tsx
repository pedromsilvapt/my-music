import {Box, Flex, Text, useMantineTheme} from '@mantine/core';
import WavesurferPlayer from "@wavesurfer/react";
import {useCallback, useMemo} from "react";
import WaveSurfer from "wavesurfer.js";
import {useWavesurferRef} from "./wavesurfer-context";

export interface PlayerTimelineProps {
    song: string;
    time: number;
    duration: number;
    setTime: (time: number) => void;
    setIsPlaying: (isPlaying: boolean) => void;
    onLoad: (duration: number) => void;
    onFinish: () => void;
    onError: (error: Error) => void;
}

export default function PlayerTimeline(props: PlayerTimelineProps) {
    const {song, time, duration, setTime, setIsPlaying, onLoad, onFinish, onError} = props;
    const theme = useMantineTheme();
    const wavesurferRef = useWavesurferRef();

    const onReady = useCallback((ws: WaveSurfer) => {
        wavesurferRef.current = ws;
        onLoad(ws.getDuration());
        setIsPlaying(false);
    }, [wavesurferRef, onLoad, setIsPlaying]);

    const onTimeUpdate = useCallback((_: WaveSurfer, newTime: number) => {
        setTime(newTime);
    }, [setTime]);

    const handleError = useCallback((_: WaveSurfer, error: Error) => {
        console.error('[PlayerTimeline] WaveSurfer error:', error);
        onError(error);
    }, [onError]);

    const timeDisplay = useMemo(() => {
        const minutes = Math.floor(time / 60).toString().padStart(2, "0");
        const seconds = Math.floor(time % 60).toString().padStart(2, "0");

        return `${minutes}:${seconds}`;
    }, [time]);

    const durationDisplay = useMemo(() => {
        const minutes = Math.floor(duration / 60).toString().padStart(2, "0");
        const seconds = Math.floor(duration % 60).toString().padStart(2, "0");

        return `${minutes}:${seconds}`;
    }, [duration]);

    return <Flex
        flex="1 1 auto"
        gap="sm"
        justify="center"
        align="center"
        direction="row"
    >
        <Text>{timeDisplay}</Text>
        <Box flex="1 1 auto"
             maw="400px"
             miw="100px"
             style={{cursor: "pointer"}}>
            <WavesurferPlayer
                height={30}
                waveColor={theme.colors.gray[3]}
                progressColor={theme.colors.blue[5]}
                cursorWidth={0}
                barGap={1}
                barRadius={2}
                barWidth={2}
                url={song}
                onReady={onReady}
                onTimeupdate={onTimeUpdate}
                onPlay={() => setIsPlaying(true)}
                onPause={() => setIsPlaying(false)}
                onFinish={() => onFinish()}
                onError={handleError}
            />
        </Box>
        <Text>{durationDisplay}</Text>
    </Flex>;
}
