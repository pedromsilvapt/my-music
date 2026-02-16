import {Center, Divider, Flex, Group} from '@mantine/core';
import {usePrevious} from "@mantine/hooks";
import React, {useCallback, useEffect, useState} from "react";
import WaveSurfer from 'wavesurfer.js';
import {usePlayerContext} from "../../contexts/player-context.tsx";
import PlayerControls from "./player-controls.tsx";
import PlayerInfo from "./player-info.tsx";
import PlayerTimeline from "./player-timeline.tsx";
import PlayerVolume from "./player-volume.tsx";

export default function Player() {
    const [wavesurfer, setWavesurfer] = useState<WaveSurfer | null>(null);
    const playerStore = usePlayerContext(); // TODO Refactor this into smaller components, each selecting only the state they need from the store to avoid excessive re-renders
    const previousSong = usePrevious(playerStore.current);
    
    const loadWaveSurfer = useCallback((ws: WaveSurfer) => {
        setWavesurfer(ws);
        ws.setVolume(playerStore.output.volume);
        ws.setMuted(playerStore.output.muted);

        playerStore.load(ws.getDuration());

        if (playerStore.current.type === 'LOADED') {
            ws.setTime(playerStore.current.time);
        }

        if (playerStore.autoplay) {
            ws.play();
        }
    }, [playerStore]);

    const userSetIsPlaying = useCallback((isPlaying: boolean) => {
        playerStore.setIsPlaying(isPlaying);
        if (isPlaying) {
            wavesurfer?.play();
        } else {
            wavesurfer?.pause();
        }
    }, [wavesurfer]);

    const userSetVolume = useCallback((volume: number) => {
        playerStore.setVolume(volume);
        wavesurfer?.setVolume(volume);
    }, [wavesurfer]);

    const userSetIsMuted = useCallback((isMuted: boolean) => {
        playerStore.setMuted(isMuted);
        wavesurfer?.setMuted(isMuted);
    }, [wavesurfer]);

    useEffect(() => {
        if (wavesurfer != null &&
            previousSong != null &&
            playerStore.current.type === 'LOADING' &&
            previousSong.type === 'LOADED' &&
            playerStore.current.song.id === previousSong.song.id
        ) {
            wavesurfer.setTime(0);
            playerStore.load(previousSong.song.id);
        }
    }, [wavesurfer, playerStore.current, previousSong]);
    
    let controls: React.ReactNode;

    if (playerStore.current.type == 'EMPTY') {
        controls = <Center flex={1}>

        </Center>;
    } else {
        let duration = 0,
            time = 0,
            isPlaying = false;

        if (playerStore.current.type == 'LOADED') {
            duration = playerStore.current.duration;
            time = playerStore.current.time;
            isPlaying = playerStore.current.isPlaying;
        }

        controls = <>
            <Group>
                <PlayerControls
                    isPlaying={isPlaying}
                    setIsPlaying={userSetIsPlaying}
                    hasNext={playerStore.current.song.order < playerStore.queue.length - 1}
                    hasPrevious={playerStore.current.song.order > 0}
                    playPrevious={playerStore.goBackward}
                    playNext={playerStore.goForward}
                />

                <Divider orientation="vertical" style={{marginTop: 15, marginBottom: 15}}/>

                <PlayerInfo
                    artwork={playerStore.current.song.cover}
                    title={playerStore.current.song.title}
                    album={playerStore.current.song.album.name}
                    artists={playerStore.current.song.artists.map(a => a.name)}
                    year={playerStore.current.song.year}
                    isExplicit={playerStore.current.song.isExplicit}
                    isFavorite={playerStore.current.song.isFavorite}
                    setIsFavorite={playerStore.setIsFavorite}
                    id={playerStore.current.song.id}
                />
            </Group>

            {/*<Box flex={1}></Box>*/}

            <PlayerTimeline
                song={`/api/songs/${playerStore.current.song.id}/download`}
                setWavesurfer={loadWaveSurfer}
                setIsPlaying={playerStore.setIsPlaying}
                duration={duration}
                time={time} setTime={playerStore.setCurrentTime}
                onFinish={playerStore.goForward}
            />
        </>;
    }


    return <>
        <Flex
            style={{marginLeft: 10, marginRight: 10}}
            h="100%"
            gap="md"
            justify="space-between"
            align="center"
            direction="row"
            wrap="wrap"
        >
            {...React.Children.toArray(controls)}

            {/*<Box flex={1}></Box>*/}

            <PlayerVolume
                volume={playerStore.output.volume} setVolume={userSetVolume}
                isMuted={playerStore.output.muted} setIsMuted={userSetIsMuted}/>
        </Flex>
    </>;
}
