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
    const {isPlaying, setIsPlaying, hasPrevious, hasNext, playPrevious, playNext} = props;
    const onPlayPause = useCallback(() => {
        setIsPlaying(!isPlaying);
    }, [setIsPlaying, isPlaying]);

    return <>
        <Group>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label="Play Previous Track in Queue"
                title="Previous"
                disabled={!hasPrevious}
                onClick={playPrevious}
            >
                <IconPlayerSkipBack/>
            </ActionIcon>
            <ActionIcon
                variant="default"
                size="xl"
                aria-label={isPlaying ? "Pause Current Track" : "Play Current Track"}
                title={isPlaying ? "Pause" : "Play"}
                onClick={onPlayPause}
            >
                {isPlaying ? <IconPlayerPause/> : <IconPlayerPlay/>}
            </ActionIcon>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label="Play Next Track in Queue"
                title="Next"
                disabled={!hasNext}
                onClick={playNext}
            >
                <IconPlayerSkipForward/>
            </ActionIcon>
        </Group>
    </>;
}
