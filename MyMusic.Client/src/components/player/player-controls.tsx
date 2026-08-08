import {ActionIcon, Group} from '@mantine/core';
import {IconPlayerPause, IconPlayerPlay, IconPlayerSkipBack, IconPlayerSkipForward} from "@tabler/icons-react";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";

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
    const {t} = useTranslation(["player", "common"]);
    const onPlayPause = useCallback(() => {
        setIsPlaying(!isPlaying);
    }, [setIsPlaying, isPlaying]);

    return <>
        <Group>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label={t("player:controls.playPreviousTrack")}
                title={t("player:controls.previous")}
                disabled={!hasPrevious}
                onClick={playPrevious}
            >
                <IconPlayerSkipBack/>
            </ActionIcon>
            <ActionIcon
                variant="default"
                size="xl"
                aria-label={isPlaying ? t("player:controls.pauseCurrentTrack") : t("player:controls.playCurrentTrack")}
                title={isPlaying ? t("player:controls.pause") : t("player:controls.play")}
                onClick={onPlayPause}
            >
                {isPlaying ? <IconPlayerPause/> : <IconPlayerPlay/>}
            </ActionIcon>
            <ActionIcon
                variant="default"
                size="lg"
                aria-label={t("player:controls.playNextTrack")}
                title={t("player:controls.next")}
                disabled={!hasNext}
                onClick={playNext}
            >
                <IconPlayerSkipForward/>
            </ActionIcon>
        </Group>
    </>;
}
