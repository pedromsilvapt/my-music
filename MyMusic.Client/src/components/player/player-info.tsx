import {ActionIcon, Box, Group, Text} from '@mantine/core';
import Artwork from "../common/artwork.tsx";
import {IconDotsVertical, IconHeart, IconHeartFilled, IconMusic, IconPlaylistAdd} from "@tabler/icons-react";
import ExplicitLabel from "../common/explicit-label.tsx";

export interface PlayerInfoProps {
    title: string;
    artists: string[];
    album: string;
    year: number | null | undefined;
    artwork: number | null;
    isFavorite: boolean;
    setIsFavorite: (isFavorite: boolean) => void;
    isExplicit: boolean;
}

export default function PlayerInfo(props: PlayerInfoProps) {
    return <>
        <Group>
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
            <Group gap="sm">
                <ActionIcon
                    variant="default"
                    size="lg"
                    aria-label={props.isFavorite ? "Favorite" : "Unfavorite"}
                    title={props.isFavorite ? "Favorite" : "Unfavorite"}
                    onClick={() => props.setIsFavorite(!props.isFavorite)}
                >
                    {props.isFavorite ? <IconHeartFilled/> : <IconHeart/>}
                </ActionIcon>
                <ActionIcon
                    variant="default"
                    size="lg"
                    aria-label="Add to Playlists"
                    title="Add to Playlists"
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
