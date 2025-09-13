import {ActionIcon, Group} from '@mantine/core';
import {IconPlayerPause, IconPlayerPlay, IconPlayerSkipBack, IconPlayerSkipForward} from "@tabler/icons-react";
import {useCallback} from "react";

export interface PlayerControlsProps {
    isPlaying: boolean;
    setIsPlaying: (isPlaying: boolean) => void;
    hasPrevious: boolean;
    hasNext: boolean;
    playPrevious: () => void;
    playNext: () => void;
}

export default function PlayerControls(props: PlayerControlsProps) {
    const onPlayPause = useCallback(() => {
        props.setIsPlaying(!props.isPlaying);
    }, [props.setIsPlaying, props.isPlaying]);

    return <>
        <Group>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label="Play Previous Track in Queue"
                title="Previous"
                disabled={!props.hasPrevious}
                onClick={props.playPrevious}
            >
                <IconPlayerSkipBack/>
            </ActionIcon>
            <ActionIcon
                variant="default"
                size="xl"
                aria-label={props.isPlaying ? "Pause Current Track" : "Play Current Track"}
                title={props.isPlaying ? "Pause" : "Play"}
                onClick={onPlayPause}
            >
                {props.isPlaying ? <IconPlayerPause/> : <IconPlayerPlay/>}
            </ActionIcon>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label="Play Next Track in Queue"
                title="Next"
                disabled={!props.hasNext}
                onClick={props.playNext}
            >
                <IconPlayerSkipForward/>
            </ActionIcon>
        </Group>
    </>;
}
