import {ActionIcon, Box, Group, Text, UnstyledButton} from '@mantine/core';
import {Link} from '@tanstack/react-router';
import Artwork from "../common/artwork.tsx";
import {IconDotsVertical, IconHeart, IconHeartFilled, IconMusic, IconPlaylistAdd} from "@tabler/icons-react";
import ExplicitLabel from "../common/explicit-label.tsx";
import {useToggleFavorite} from "../../hooks/use-favorites.ts";
import {useManagePlaylistsContext} from "../../contexts/manage-playlists-context.tsx";
import {usePlaybackActions} from "../../stores/playback-store";
import {useQueuesMutations, useQueueList} from "../../hooks/use-queues";
import {useCallback} from "react";
import styles from './player-info.module.css';

export interface PlayerInfoProps {
    title: string;
    artists: string[];
    album: string;
    year: number | null | undefined;
    artwork: number | null;
    isFavorite: boolean;
    setIsFavorite: (isFavorite: boolean, songId?: number) => void;
    isExplicit: boolean;
    id: number;
}

export default function PlayerInfo(props: PlayerInfoProps) {
    const handleFavoriteSuccess = useCallback((data: { data: { isFavorite: boolean } }) => {
        props.setIsFavorite(data.data.isFavorite, props.id);
    }, [props]);
    
    const toggleFavorite = useToggleFavorite(handleFavoriteSuccess);
    const {open: openManagePlaylists} = useManagePlaylistsContext();
    const {requestScrollToCurrent} = usePlaybackActions((s) => ({
        requestScrollToCurrent: s.requestScrollToCurrent,
    }));
    const {viewQueue} = useQueuesMutations();
    const {visibleQueueId, currentQueueId} = useQueueList();

    const handleSongInfoClick = () => {
        if (currentQueueId !== null && visibleQueueId !== currentQueueId) {
            viewQueue(currentQueueId);
        }
        requestScrollToCurrent();
    };

    return <>
        <Group>
            <Link to="/player" className={styles.songInfoLink} onClick={handleSongInfoClick}>
                <UnstyledButton className={styles.songInfoButton}>
                    <Group gap="sm">
                        <Artwork id={props.artwork} size={60} placeholderIcon={<IconMusic/>}/>
                        <Box>
                            <ExplicitLabel visible={props.isExplicit}>
                                <Text size="sm">{props.title}</Text>
                            </ExplicitLabel>
                            <Text size="xs" opacity={0.5}>
                                {props.artists[0]} • {props.album} • {props.year}
                            </Text>
                        </Box>
                    </Group>
                </UnstyledButton>
            </Link>
            <Group gap="sm">
                <ActionIcon
                    variant="default"
                    size="lg"
                    aria-label={props.isFavorite ? "Favorite" : "Unfavorite"}
                    title={props.isFavorite ? "Favorite" : "Unfavorite"}
                    onClick={() => toggleFavorite.mutate({id: props.id})}
                >
                    {props.isFavorite ? <IconHeartFilled/> : <IconHeart/>}
                </ActionIcon>
                <ActionIcon
                    variant="default"
                    size="lg"
                    aria-label="Add to Playlists"
                    title="Add to Playlists"
                    onClick={() => openManagePlaylists([props.id])}
                >
                    <IconPlaylistAdd/>
                </ActionIcon>
                <ActionIcon
                    variant="default"
                    size="lg"
                    aria-label="Song Actions"
                    title="Song Actions"
                >
                    <IconDotsVertical/>
                </ActionIcon>
            </Group>
        </Group>
    </>;
}
