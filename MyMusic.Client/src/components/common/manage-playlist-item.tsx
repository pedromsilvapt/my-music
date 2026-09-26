import {Card, Group, Text, useComputedColorScheme} from "@mantine/core";
import {IconPlaylist} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";
import type {ListPlaylistItem} from "../../model";

export interface ManagePlaylistItemProps {
    playlist: ListPlaylistItem;
    isIncluded: boolean;
}

/**
 * Playlist counterpart of {@link ManageSongItem}: a card with a green left border when included.
 */
export default function ManagePlaylistItem({playlist, isIncluded}: ManagePlaylistItemProps) {
    const {t} = useTranslation(["common"]);
    const colorScheme = useComputedColorScheme('light');

    const border = colorScheme === 'dark'
        ? (isIncluded ? 'var(--mantine-color-green-5)' : 'var(--mantine-color-dark-4)')
        : (isIncluded ? 'var(--mantine-color-green-6)' : 'var(--mantine-color-gray-4)');

    return (
        <Card data-testid={`manage-playlist-item-${playlist.id}`} data-included={isIncluded} padding="xs" radius="sm"
              style={{borderLeft: `3px solid ${border}`}}>
            <Group gap="xs" wrap="nowrap">
                <IconPlaylist size={14} style={{flexShrink: 0}}/>
                <Text size="sm" truncate style={{whiteSpace: 'nowrap'}}>
                    <Text component="span" fw={500}>{playlist.name}</Text>
                    <Text component="span" c="dimmed"> • {t("common:count.songs", {count: playlist.songCount})}</Text>
                </Text>
            </Group>
        </Card>
    );
}
