import {Card, List, Text, ThemeIcon} from "@mantine/core";
import {IconCheck} from '@tabler/icons-react';
import type {TFunction} from "i18next";
import {useTranslation} from "react-i18next";
import type {SoundalikeSongItem} from "../../model/soundalikeSongItem.ts";
import {SecondaryAction} from "../../model/secondaryAction.ts";
import type {GroupSelection} from "./soundalike-selection.ts";

function getMergedMetadataPreview(t: TFunction<["audits", "common"]>, primarySong: SoundalikeSongItem, secondarySongs: SoundalikeSongItem[]) {
    const changes: string[] = [];

    if (!primarySong.year && secondarySongs.some(s => s.year)) {
        const year = secondarySongs.find(s => s.year)?.year;
        if (year) changes.push(t("audits:soundalike.preview.year", {value: year}));
    }

    if (!primarySong.hasLyrics && secondarySongs.some(s => s.hasLyrics)) {
        changes.push(t("audits:soundalike.preview.lyrics"));
    }

    if (!primarySong.cover && secondarySongs.some(s => s.cover)) {
        changes.push(t("audits:soundalike.preview.artwork"));
    }

    if (!primarySong.bitrate && secondarySongs.some(s => s.bitrate)) {
        const bitrate = secondarySongs.find(s => s.bitrate)?.bitrate;
        if (bitrate) changes.push(t("audits:soundalike.preview.bitrate", {value: bitrate}));
    }

    const newGenres = secondarySongs.flatMap(s => s.genres)
        .filter(g => !primarySong.genres.some(pg => pg.id === g.id));
    if (newGenres.length > 0) {
        changes.push(t("audits:soundalike.preview.genres", {value: newGenres.map(g => g.name).join(', ')}));
    }

    const newArtists = secondarySongs.flatMap(s => s.artists)
        .filter(a => !primarySong.artists.some(pa => pa.id === a.id));
    if (newArtists.length > 0) {
        changes.push(t("audits:soundalike.preview.artists", {value: newArtists.map(a => a.name).join(', ')}));
    }

    return changes;
}

interface SoundalikeResolutionSummaryProps {
    songs: SoundalikeSongItem[];
    selection: GroupSelection;
}

/**
 * What resolving a group of soundalikes will do: the metadata the kept song gains, and what happens to the others.
 */
export default function SoundalikeResolutionSummary({songs, selection}: SoundalikeResolutionSummaryProps) {
    const {t} = useTranslation(["audits", "common"]);

    const primarySong = songs.find(s => s.id === selection.primaryId);
    if (!primarySong) return null;

    const mergeSongs = songs.filter(s => selection.actions.get(s.id) === SecondaryAction.Merge);
    const deleteSongs = songs.filter(s => selection.actions.get(s.id) === SecondaryAction.Delete);
    const ignoreSongs = songs.filter(s => selection.actions.get(s.id) === SecondaryAction.Ignore);

    const changes = mergeSongs.length > 0 ? getMergedMetadataPreview(t, primarySong, mergeSongs) : [];

    return (
        <Card withBorder padding="sm" data-testid="soundalike-resolution-summary">
            <Text fw={600} mb="xs">{primarySong.title}</Text>
            {changes.length > 0 ? (
                <List size="sm" spacing={2} mb="xs">
                    {changes.map((change) => (
                        <List.Item key={change} icon={
                            <ThemeIcon color="blue" size="sm" radius="xl">
                                <IconCheck size={12} />
                            </ThemeIcon>
                        }>
                            {change}
                        </List.Item>
                    ))}
                </List>
            ) : null}
            {deleteSongs.length > 0 && (
                <Text size="sm" c="dimmed">
                    {t("audits:soundalike.deleting", {titles: deleteSongs.map(s => s.title).join(', ')})}
                </Text>
            )}
            {mergeSongs.length > 0 && (
                <Text size="sm" c="dimmed">
                    {t("audits:soundalike.mergingThenDeleting", {titles: mergeSongs.map(s => s.title).join(', ')})}
                </Text>
            )}
            {ignoreSongs.length > 0 && (
                <Text size="sm" c="dimmed">
                    {t("audits:soundalike.ignoring", {titles: ignoreSongs.map(s => s.title).join(', ')})}
                </Text>
            )}
        </Card>
    );
}
