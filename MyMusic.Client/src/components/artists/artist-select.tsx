import {Group, Stack, Text} from "@mantine/core";
import {IconUser} from "@tabler/icons-react";
import {useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useListArtists} from "../../client/artists.ts";
import type {ListArtistItem} from "../../model";
import Artwork from "../common/artwork.tsx";
import VirtualSelect from "../common/virtual-select.tsx";

const ARTIST_OPTION_HEIGHT = 56;
const ARTIST_ARTWORK_SIZE = 40;

interface ArtistSelectProps {
    value: ListArtistItem | null;
    onChange: (artist: ListArtistItem) => void;
    label?: string;
    placeholder?: string;
    error?: React.ReactNode;
    required?: boolean;
    disabled?: boolean;
    /** The artists are only fetched while enabled (e.g. while the enclosing modal is open). */
    enabled?: boolean;
    testId?: string;
}

const getArtistKey = (artist: ListArtistItem) => artist.id;
const getArtistLabel = (artist: ListArtistItem) => artist.name;

/**
 * Picks one of the user's artists. Every artist is listed (the dropdown is virtualized); artist names are not
 * unique, so each option also shows the artist's album and song counts.
 */
export default function ArtistSelect(props: ArtistSelectProps) {
    const {t} = useTranslation(["artists", "common"]);

    const artistsQuery = useListArtists(undefined, {
        query: {
            enabled: props.enabled ?? true,
            select: response => response.data.artists,
        },
    });

    const artists = useMemo(
        () => [...(artistsQuery.data ?? [])].sort((a, b) => a.name.localeCompare(b.name)),
        [artistsQuery.data]);

    const renderOption = useCallback((artist: ListArtistItem) => (
        <Group gap="sm" wrap="nowrap" h="100%">
            <Artwork id={artist.photo} size={ARTIST_ARTWORK_SIZE} placeholderIcon={<IconUser size={20}/>}
                     enablePreview={false}/>
            <Stack gap={0} style={{minWidth: 0}}>
                <Text size="sm" lineClamp={1}>{artist.name}</Text>
                <Text size="xs" c="dimmed" lineClamp={1}>
                    {t("common:count.albums", {count: artist.albumsCount ?? 0})}
                    {", "}
                    {t("common:count.songs", {count: artist.songsCount ?? 0})}
                </Text>
            </Stack>
        </Group>
    ), [t]);

    return (
        <VirtualSelect
            items={artists}
            value={props.value}
            onChange={props.onChange}
            getKey={getArtistKey}
            getLabel={getArtistLabel}
            renderOption={renderOption}
            optionHeight={ARTIST_OPTION_HEIGHT}
            emptyMessage={t("artists:select.empty")}
            label={props.label}
            placeholder={props.placeholder ?? t("artists:select.placeholder")}
            error={props.error}
            required={props.required}
            disabled={props.disabled}
            loading={artistsQuery.isFetching}
            testId={props.testId}
        />
    );
}
