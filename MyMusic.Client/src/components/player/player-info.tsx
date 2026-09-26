import {Box, Group, Text, UnstyledButton} from '@mantine/core';
import {Link, useNavigate} from '@tanstack/react-router';
import {useTranslation} from 'react-i18next';
import Artwork from "../common/artwork.tsx";
import {IconInfoCircle, IconMusic} from "@tabler/icons-react";
import ExplicitLabel from "../common/explicit-label.tsx";
import {useQueuesMutations, useQueueList} from "../../hooks/use-queues";
import {useQueue} from "../../contexts/player-context";
import {usePlaybackActions} from "../../stores/playback-store";
import styles from './player-info.module.css';
import type {GetPlaylistSongItem, ListSongItem} from '../../model';
import {useSongsSchema} from '../songs/useSongsSchema';
import CollectionActions from '../common/collection/collection-actions';
import type {CollectionSchemaAction} from '../common/collection/collection-schema.tsx';
import {getFooterSongActions} from './player-info-actions';

export interface PlayerInfoProps {
    song: GetPlaylistSongItem;
    setIsFavorite: (isFavorite: boolean, songId?: number) => void;
}

export default function PlayerInfo(props: PlayerInfoProps) {
    const {t} = useTranslation(["songs"]);
    const navigate = useNavigate();
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

    // The playback store holds a snapshot of the song; the queue has its up-to-date playback flags
    const {queue, queueId} = useQueue();
    const song = (queue.find(s => s.id === props.song.id) ?? props.song) as ListSongItem;

    const schema = useSongsSchema(true, {queueId});
    const goToDetails: CollectionSchemaAction<ListSongItem> = {
        name: 'go-to-details',
        renderIcon: () => <IconInfoCircle/>,
        renderLabel: () => t("songs:schema.goToDetails"),
        onClick: (songs: ListSongItem[]) => {
            navigate({to: '/songs/$songId', params: {songId: String(songs[0]!.id)}});
        },
    };
    const actions = getFooterSongActions([goToDetails, ...(schema.actions?.([song]) ?? [])]);

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
            <CollectionActions selection={[song]} actions={actions} size="lg" />
        </Group>
    </>;
}
