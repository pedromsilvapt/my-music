import {ActionIcon, Flex, Slider} from '@mantine/core';
import {IconVolume, IconVolumeOff} from "@tabler/icons-react";

export interface PlayerVolumeProps {
    isMuted: boolean;
    setIsMuted: (muted: boolean) => void;
    volume: number;
    setVolume: (volume: number) => void;
}

export default function PlayerVolume(props: PlayerVolumeProps) {
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
                aria-label={props.isMuted ? "Unmute" : "Mute"}
                title={props.isMuted ? "Unmute" : "Mute"}
                onClick={() => props.setIsMuted(!props.isMuted)}
            >
                {props.isMuted ? <IconVolumeOff size="1.1rem"/> : <IconVolume size="1.1rem"/>}
            </ActionIcon>
            <Slider flex={1} value={props.volume * 100} onChange={v => props.setVolume(Math.min(1, v / 100))}/>
        </Flex>
    </>;
}
