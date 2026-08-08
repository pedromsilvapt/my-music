import {ActionIcon, Flex, Slider} from '@mantine/core';
import {IconVolume, IconVolumeOff} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";

export interface PlayerVolumeProps {
    isMuted: boolean;
    setIsMuted: (muted: boolean) => void;
    volume: number;
    setVolume: (volume: number) => void;
    onVolumeChangeEnd?: (volume: number) => void;
}

export default function PlayerVolume(props: PlayerVolumeProps) {
    const {t} = useTranslation(["player", "common"]);
    return <>
        <Flex
            style={{flex: "1 1 auto", maxWidth: "140px"}}
            gap="sm"
            justify="center"
            align="center"
            direction="row"
        >
            <ActionIcon
                variant="default"
                size="md"
                aria-label={props.isMuted ? t("player:volume.unmute") : t("player:volume.mute")}
                title={props.isMuted ? t("player:volume.unmute") : t("player:volume.mute")}
                onClick={() => props.setIsMuted(!props.isMuted)}
            >
                {props.isMuted ? <IconVolumeOff size="1.1rem"/> : <IconVolume size="1.1rem"/>}
            </ActionIcon>
            <Slider flex={1} value={props.volume * 100} onChange={v => props.setVolume(Math.min(1, v / 100))} onChangeEnd={v => props.onVolumeChangeEnd?.(Math.min(1, v / 100))} label={(value) => Math.round(value)}/>
        </Flex>
    </>;
}
