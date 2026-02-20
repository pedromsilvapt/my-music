import {Center, Divider, Flex, Group} from '@mantine/core';
import {usePlaybackStore} from '../../stores/playback-store';
import PlayerControlsContainer from './player-controls-container';
import PlayerInfoContainer from './player-info-container';
import PlayerTimelineContainer from './player-timeline-container';
import PlayerVolumeContainer from './player-volume-container';
import {WavesurferProvider} from './wavesurfer-context';

function PlayerEmpty() {
    return <Center flex={1}></Center>;
}

function PlayerActive() {
    return (
        <>
            <Group>
                <PlayerControlsContainer/>
                <Divider orientation="vertical" style={{marginTop: 15, marginBottom: 15}}/>
                <PlayerInfoContainer/>
            </Group>
            <PlayerTimelineContainer/>
        </>
    );
}

export default function Player() {
    const currentType = usePlaybackStore((s) => s.current.type);

    return (
        <WavesurferProvider>
            <Flex
                style={{marginLeft: 10, marginRight: 10}}
                h="100%"
                gap="md"
                justify="space-between"
                align="center"
                direction="row"
                wrap="wrap"
            >
                {currentType === 'EMPTY' ? <PlayerEmpty/> : <PlayerActive/>}
                <PlayerVolumeContainer/>
            </Flex>
        </WavesurferProvider>
    );
}
