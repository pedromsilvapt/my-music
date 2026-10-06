import {Card, Group, Text, TextInput, Tooltip, useComputedColorScheme} from "@mantine/core";
import {IconPointFilled} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";
import type {ListSongItem} from "../../model";

export interface ManageSongItemProps {
    song: ListSongItem;
    isIncluded: boolean;
    path?: string | null;
    syncAction?: string | null;
    /** When given, the path is shown in a text input the user can edit, instead of as text. */
    onPathChange?: (path: string) => void;
    pathLabel?: string;
    /** A note shown under the path. */
    pathHint?: string | null;
}

export default function ManageSongItem({song, isIncluded, path, syncAction, onPathChange, pathLabel, pathHint}: ManageSongItemProps) {
    const {t} = useTranslation(["common"]);
    const colorScheme = useComputedColorScheme('light');
    const artistsText = song.artists.map(a => a.name).join(', ');
    const titleArtistsText = `${song.title} • ${artistsText}`;

    const border = colorScheme === 'dark'
        ? (isIncluded ? 'var(--mantine-color-green-5)' : 'var(--mantine-color-dark-4)')
        : (isIncluded ? 'var(--mantine-color-green-6)' : 'var(--mantine-color-gray-4)');

    const actionColor = getActionColor(syncAction, colorScheme === 'light');
    const showPath = !!path || !!onPathChange;

    return (
        <Card data-testid={`manage-song-item-${song.id}`} data-included={isIncluded} padding="xs" radius="sm" style={{borderLeft: `3px solid ${border}`}}>
            <Tooltip label={titleArtistsText} openDelay={500} withinPortal={false}>
                <Text size="sm" truncate mb={showPath ? "xs" : undefined} style={{whiteSpace: 'nowrap'}}>
                    <Text component="span" fw={500} data-testid="song-title">{song.title}</Text>
                    <Text component="span"> • {artistsText}</Text>
                </Text>
            </Tooltip>
            {showPath && (
                <Card.Section withBorder p="xs">
                    <Group justify="space-between" wrap="nowrap" gap="xs">
                        {onPathChange ? (
                            <TextInput
                                data-testid="song-path-input"
                                aria-label={pathLabel}
                                size="xs"
                                value={path ?? ""}
                                onChange={(e) => onPathChange(e.currentTarget.value)}
                                style={{flex: 1, minWidth: 0}}
                                styles={{input: {fontFamily: 'monospace'}}}
                            />
                        ) : (
                            <Tooltip label={path} openDelay={500} withinPortal={false}>
                                <Text data-testid="song-path" size="xs" truncate c="dimmed" style={{fontFamily: 'monospace'}}>{path}</Text>
                            </Tooltip>
                        )}
                        {syncAction && actionColor && (
                            <Tooltip label={syncAction === 'Remove' ? t("common:common.toDelete") : t("common:common.toAction", {action: syncAction})} withinPortal={false}>
                                <IconPointFilled data-testid="sync-action" data-action={syncAction} size={14} color={actionColor} style={{flexShrink: 0}}/>
                            </Tooltip>
                        )}
                    </Group>
                    {pathHint && (
                        <Text data-testid="song-path-hint" size="xs" c="dimmed" mt={4}>{pathHint}</Text>
                    )}
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
