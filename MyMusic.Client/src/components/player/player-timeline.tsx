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
}

export default function PlayerTimeline(props: PlayerTimelineProps) {
    const theme = useMantineTheme();
    const wavesurferRef = useWavesurferRef();

    const onReady = useCallback((ws: WaveSurfer) => {
        wavesurferRef.current = ws;
        props.onLoad(ws.getDuration());
        props.setIsPlaying(false);
    }, [wavesurferRef, props.onLoad, props.setIsPlaying]);

    const onTimeUpdate = useCallback((_: WaveSurfer, time: number) => {
        props.setTime(time);
    }, [props.setTime]);

    const timeDisplay = useMemo(() => {
        const minutes = Math.floor(props.time / 60).toString().padStart(2, "0");
        const seconds = Math.floor(props.time % 60).toString().padStart(2, "0");

        return `${minutes}:${seconds}`;
    }, [props.time]);

    const durationDisplay = useMemo(() => {
        const minutes = Math.floor(props.duration / 60).toString().padStart(2, "0");
        const seconds = Math.floor(props.duration % 60).toString().padStart(2, "0");

        return `${minutes}:${seconds}`;
    }, [props.duration]);

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
                url={props.song}
                onReady={onReady}
                onTimeupdate={onTimeUpdate}
                onPlay={() => props.setIsPlaying(true)}
                onPause={() => props.setIsPlaying(false)}
                onFinish={() => props.onFinish()}
            />
        </Box>
        <Text>{durationDisplay}</Text>
    </Flex>;
}
