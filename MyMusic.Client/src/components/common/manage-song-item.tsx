import {Card, Group, Text, Tooltip, useComputedColorScheme} from "@mantine/core";
import {IconPointFilled} from "@tabler/icons-react";
import type {ListSongItem} from "../../model";

export interface ManageSongItemProps {
    song: ListSongItem;
    isIncluded: boolean;
    path?: string | null;
    syncAction?: string | null;
}

export default function ManageSongItem({song, isIncluded, path, syncAction}: ManageSongItemProps) {
    const colorScheme = useComputedColorScheme('light');
    const artistsText = song.artists.map(a => a.name).join(', ');
    const titleArtistsText = `${song.title} • ${artistsText}`;

    const border = colorScheme === 'dark'
        ? (isIncluded ? 'var(--mantine-color-green-5)' : 'var(--mantine-color-dark-4)')
        : (isIncluded ? 'var(--mantine-color-green-6)' : 'var(--mantine-color-gray-4)');

    const actionColor = getActionColor(syncAction, colorScheme === 'light');

    return (
        <Card padding="xs" radius="sm" style={{borderLeft: `3px solid ${border}`}}>
            <Tooltip label={titleArtistsText} openDelay={500} withinPortal={false}>
                <Text size="sm" truncate mb={path ? "xs" : undefined} style={{whiteSpace: 'nowrap'}}>
                    <Text component="span" fw={500}>{song.title}</Text>
                    <Text component="span"> • {artistsText}</Text>
                </Text>
            </Tooltip>
            {path && (
                <Card.Section withBorder p="xs">
                    <Group justify="space-between" wrap="nowrap" gap="xs">
                        <Tooltip label={path} openDelay={500} withinPortal={false}>
                            <Text size="xs" truncate c="dimmed" style={{fontFamily: 'monospace'}}>{path}</Text>
                        </Tooltip>
                        {syncAction && actionColor && (
                            <Tooltip label={syncAction === 'Remove' ? 'To Delete' : `To ${syncAction}`} withinPortal={false}>
                                <IconPointFilled size={14} color={actionColor} style={{flexShrink: 0}}/>
                            </Tooltip>
                        )}
                    </Group>
                </Card.Section>
            )}
        </Card>
    );
}

function getActionColor(syncAction: string | null | undefined, isLightColor: boolean) {
    return syncAction === 'Download' ? (isLightColor ? 'var(--mantine-color-green-9)' : 'var(--mantine-color-green-3)') :
        syncAction === 'Remove' ? (isLightColor ? 'red' : 'var(--mantine-color-red-3)') :
            undefined;
}
