import {Box, Group, Text, UnstyledButton} from '@mantine/core';
import {Link} from '@tanstack/react-router';
import Artwork from "../common/artwork.tsx";
import {IconMusic} from "@tabler/icons-react";
import ExplicitLabel from "../common/explicit-label.tsx";
import {useQueuesMutations, useQueueList} from "../../hooks/use-queues";
import {usePlaybackActions} from "../../stores/playback-store";
import styles from './player-info.module.css';
import type {GetPlaylistSongItem, ListSongItem} from '../../model';
import {useSongsSchema} from '../songs/useSongsSchema';
import CollectionActions from '../common/collection/collection-actions';

export interface PlayerInfoProps {
    song: GetPlaylistSongItem;
    setIsFavorite: (isFavorite: boolean, songId?: number) => void;
}

export default function PlayerInfo(props: PlayerInfoProps) {
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

    const schema = useSongsSchema(false);
    const allActions = schema.actions?.([props.song as ListSongItem]) ?? [];
    const actions = allActions.filter(action => {
        if ('group' in action && action.group === 'Queue') return false;
        if ('name' in action && ['play', 'play-next', 'play-last', 'shuffle', 'remove-from-queue'].includes(action.name)) return false;
        return true;
    }).map(action => {
        if ('name' in action && (action.name === 'favorite' || action.name === 'manage-playlists')) {
            return { ...action, primary: true };
        }
        return action;
    });

    return <>
        <Group>
            <Link to="/player" className={styles.songInfoLink} onClick={handleSongInfoClick}>
                <UnstyledButton className={styles.songInfoButton}>
                    <Group gap="sm">
                        <Artwork id={props.song.cover} size={60} placeholderIcon={<IconMusic/>}/>
                        <Box>
                            <ExplicitLabel visible={props.song.isExplicit}>
                                <Text size="sm">{props.song.title}</Text>
                            </ExplicitLabel>
                            <Text size="xs" opacity={0.5}>
                                {props.song.artists.map(a => a.name).join(', ')} • {props.song.album.name}{props.song.year ? ` • ${props.song.year}` : ''}
                            </Text>
                        </Box>
                    </Group>
                </UnstyledButton>
            </Link>
            <CollectionActions selection={[props.song as ListSongItem]} actions={actions} size="lg" />
        </Group>
    </>;
}
