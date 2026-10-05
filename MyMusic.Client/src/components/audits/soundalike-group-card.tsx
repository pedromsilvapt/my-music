import {ActionIcon, Badge, Card, Group, Stack, Text, Tooltip, useComputedColorScheme} from "@mantine/core";
import {IconCheck} from '@tabler/icons-react';
import {useTranslation} from "react-i18next";
import type {SoundalikeSongItem} from "../../model/soundalikeSongItem.ts";
import {SecondaryAction} from "../../model/secondaryAction.ts";
import SongArtwork from "../common/fields/song-artwork";
import ExplicitLabel from "../common/explicit-label.tsx";
import {formatFileSize} from "../../utils/format-file-size.ts";
import {formatRelativeDate} from "../../utils/format-relative-date.ts";
import type {GroupSelection, SongAction} from "./soundalike-selection.ts";

interface SoundalikeGroupCardProps {
    songs: SoundalikeSongItem[];
    /** The lowest score between any two of the songs (0 to 1), or null when it could not be computed. */
    matchScore: number | null;
    selection?: GroupSelection;
    onSelectPrimary: (songId: number) => void;
    onSetAction: (songId: number, action: SongAction) => void;
    /** Shows a button to resolve this group on its own. */
    onResolve?: () => void;
}

const SONG_ACTIONS = [SecondaryAction.Delete, SecondaryAction.Merge, SecondaryAction.Ignore] as const;

export default function SoundalikeGroupCard({songs, matchScore, selection, onSelectPrimary, onSetAction, onResolve}: SoundalikeGroupCardProps) {
    const {t} = useTranslation(["audits", "common"]);
    const colorScheme = useComputedColorScheme('light');

    const primaryBg = colorScheme === 'dark' ? 'var(--mantine-color-blue-8)' : 'var(--mantine-color-blue-1)';
    const primaryBorder = colorScheme === 'dark' ? 'var(--mantine-color-blue-4)' : 'var(--mantine-color-blue-6)';
    const primaryText = colorScheme === 'dark' ? 'var(--mantine-color-blue-0)' : undefined;
    const activeBadgeVariant = colorScheme === 'dark' ? 'white' : 'light';

    const actionBadgeProps: Record<SongAction, { color: string; label: string }> = {
        [SecondaryAction.Delete]: { color: 'red', label: t("audits:soundalike.actions.delete") },
        [SecondaryAction.Merge]: { color: 'orange', label: t("audits:soundalike.actions.merge") },
        [SecondaryAction.Ignore]: { color: 'gray', label: t("audits:soundalike.actions.ignore") },
    };

    return (
        <Card shadow="sm" padding="lg" radius="md" withBorder data-testid="soundalike-group">
            <Group justify="space-between" mb="md">
                <Text fw={500} data-testid="soundalike-match-score">
                    {matchScore == null
                        ? t("audits:soundalike.matchScoreUnavailable")
                        : t("audits:soundalike.matchScore", {score: Math.round(matchScore * 100)})}
                </Text>
                <Group gap="sm">
                    <Text size="sm" c="dimmed">
                        {t("common:count.songs", {count: songs.length})}
                    </Text>
                    {selection && onResolve && (
                        <Tooltip label={t("audits:soundalike.resolveGroup")} openDelay={500}>
                            <ActionIcon
                                variant="light"
                                color="green"
                                size="sm"
                                aria-label={t("audits:soundalike.resolveGroup")}
                                data-testid="soundalike-group-resolve"
                                onClick={onResolve}
                            >
                                <IconCheck size={14}/>
                            </ActionIcon>
                        </Tooltip>
                    )}
                </Group>
            </Group>

            <Stack gap="sm">
                {songs.map((song) => {
                    const isPrimary = selection?.primaryId === song.id;
                    const songAction = selection?.actions.get(song.id);
                    const hasSelection = !!selection;
                    // Only the songs that will be gone once the group is resolved are struck through
                    const isRemoved = songAction === SecondaryAction.Delete || songAction === SecondaryAction.Merge;

                    return (
                        <Card
                            key={song.id}
                            padding="sm"
                            withBorder
                            data-testid="soundalike-song"
                            data-primary={isPrimary ? "true" : "false"}
                            style={{
                                cursor: 'pointer',
                                backgroundColor: isPrimary ? primaryBg : undefined,
                                borderColor: isPrimary ? primaryBorder : undefined
                            }}
                            onClick={() => onSelectPrimary(song.id)}
                        >
                            <Group justify="space-between" wrap="nowrap">
                                <Group style={{minWidth: 0, flex: 1}}>
                                    <Tooltip label={`${song.coverWidth ?? '-'} × ${song.coverHeight ?? '-'}`} openDelay={500}>
                                        <SongArtwork id={song.cover} size={40} />
                                    </Tooltip>
                                    <div style={{minWidth: 0, flex: 1}}>
                                        <ExplicitLabel visible={song.isExplicit}>
                                            <Text fw={isPrimary ? 600 : 400} c={isPrimary ? primaryText : undefined} lineClamp={1} style={{textDecoration: isRemoved ? 'line-through' : undefined}}>
                                                {song.title}
                                            </Text>
                                        </ExplicitLabel>
                                        <Text size="sm" c={isPrimary ? primaryText : "dimmed"} lineClamp={1} style={{textDecoration: isRemoved ? 'line-through' : undefined}}>
                                            {song.artists.map(a => a.name).join(', ')} • {song.album?.name ?? t("audits:soundalike.unknownAlbum")}
                                        </Text>
                                        <Tooltip label={song.createdAt ? new Date(song.createdAt).toLocaleString() : undefined} disabled={!song.createdAt} openDelay={500}>
                                            <Text size="xs" c={isPrimary ? primaryText : "dimmed"} lineClamp={1} style={{textDecoration: isRemoved ? 'line-through' : undefined}}>
                                                {[
                                                    song.duration,
                                                    song.size ? formatFileSize(song.size) : null,
                                                    song.bitrate ? `${song.bitrate} kbps` : null,
                                                    song.genres.length ? song.genres.map(g => g.name).join(', ') : null,
                                                    song.createdAt ? formatRelativeDate(song.createdAt) : null,
                                                ].filter(Boolean).join(' \u2022 ')}
                                            </Text>
                                        </Tooltip>
                                    </div>
                                </Group>
                                <Group gap="xs" wrap="nowrap">
                                    {isPrimary && (
                                        <Badge color="blue" variant={activeBadgeVariant}>
                                            {t("common:common.keep")}
                                        </Badge>
                                    )}
                                    {!isPrimary && hasSelection && SONG_ACTIONS.map(action => {
                                        const props = actionBadgeProps[action];
                                        const isActive = songAction === action;
                                        return (
                                            <Badge
                                                key={action}
                                                data-testid={`soundalike-action-${action.toLowerCase()}`}
                                                data-active={isActive ? "true" : "false"}
                                                color={props.color}
                                                variant={isActive ? activeBadgeVariant : 'outline'}
                                                style={{cursor: 'pointer', opacity: isActive ? 1 : 0.5}}
                                                onClick={(e) => {
                                                    e.stopPropagation();
                                                    onSetAction(song.id, action);
                                                }}
                                            >
                                                {props.label}
                                            </Badge>
                                        );
                                    })}
                                    {song.year && <Text size="sm" c={isPrimary ? primaryText : "dimmed"}>{song.year}</Text>}
                                    {song.hasLyrics && <Badge variant="light" color="grape">{t("audits:soundalike.lyrics")}</Badge>}
                                </Group>
                            </Group>
                        </Card>
                    );
                })}
            </Stack>
        </Card>
    );
}
