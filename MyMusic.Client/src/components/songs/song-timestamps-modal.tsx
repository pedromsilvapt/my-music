import {Button, Group, Modal, Stack} from "@mantine/core";
import {DateTimePicker} from "@mantine/dates";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useGetLocalSong} from "../../client/songs.ts";
import {ZINDEX_DRAWER, ZINDEX_LIGHTBOX} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useUpdateSongTimestampsWithNotifications} from "../../hooks/use-update-song-timestamps.ts";
import type {GetSongResponseSong} from "../../model";

const TIMESTAMP_FIELDS = [
    {name: "createdAt", testId: "song-timestamp-created-at", required: true},
    {name: "modifiedAt", testId: "song-timestamp-modified-at", required: true},
    {name: "addedAt", testId: "song-timestamp-added-at", required: false},
    {name: "fileModifiedAt", testId: "song-timestamp-file-modified-at", required: false},
] as const;

type TimestampName = typeof TIMESTAMP_FIELDS[number]["name"];

/** The timestamps being edited, as ISO strings. */
type Timestamps = Record<TimestampName, string | null>;

const pad = (value: number) => String(value).padStart(2, "0");

/** Formats an ISO timestamp as the local `YYYY-MM-DD HH:mm:ss` string the Mantine date pickers work with. */
function toPickerValue(iso: string | null): string | null {
    if (!iso) return null;

    const date = new Date(iso);

    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
        `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

/** Converts the local `YYYY-MM-DD HH:mm:ss` string of a Mantine date picker to an ISO timestamp. */
function fromPickerValue(value: string | null): string | null {
    return value ? new Date(value.replace(" ", "T")).toISOString() : null;
}

interface SongTimestampsModalProps {
    opened: boolean;
    onClose: () => void;
    songId: number;
}

/**
 * Lets the user set the timestamps of a song by hand. Nothing is saved until the form is submitted.
 */
export default function SongTimestampsModal({opened, onClose, songId}: SongTimestampsModalProps) {
    const {t} = useTranslation(["songs", "common"]);

    // The song is fetched here rather than received from the edit modal, whose copy may be outdated
    const songQuery = useGetLocalSong(songId, {query: {enabled: opened}});
    const song = useQueryData(songQuery, t("songs:detailPage.fetchFailed"))?.data.song;

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title={t("songs:tools.changeTimestamps.title")}
            centered
            zIndex={ZINDEX_DRAWER}
        >
            {opened && song ? (
                // Keyed by the fetch time so the form starts over from the values of a refetched song
                <SongTimestampsForm
                    key={songQuery.dataUpdatedAt}
                    song={song}
                    isFetching={songQuery.isFetching}
                    onClose={onClose}
                />
            ) : (
                <Stack data-testid="song-timestamps-modal" data-loading="true">
                    {t("common:common.loading")}
                </Stack>
            )}
        </Modal>
    );
}

interface SongTimestampsFormProps {
    song: GetSongResponseSong;
    isFetching: boolean;
    onClose: () => void;
}

function SongTimestampsForm({song, isFetching, onClose}: SongTimestampsFormProps) {
    const {t} = useTranslation(["songs", "common"]);
    const {update, isPending} = useUpdateSongTimestampsWithNotifications();

    // Kept as ISO strings and only replaced when a field is changed, so untouched timestamps keep their precision
    const [timestamps, setTimestamps] = useState<Timestamps>({
        createdAt: song.createdAt,
        modifiedAt: song.modifiedAt,
        addedAt: song.addedAt ?? null,
        fileModifiedAt: song.fileModifiedAt ?? null,
    });

    const setTimestamp = (name: TimestampName, value: string | null) =>
        setTimestamps(current => ({...current, [name]: value}));

    const {createdAt, modifiedAt} = timestamps;
    const canSave = createdAt != null && modifiedAt != null;

    const handleSave = () => {
        if (createdAt == null || modifiedAt == null) return;

        update(song.id, {...timestamps, createdAt, modifiedAt}, onClose);
    };

    return (
        <Stack gap="md" data-testid="song-timestamps-modal" data-loading={isFetching ? "true" : "false"}>
            {TIMESTAMP_FIELDS.map(field => (
                <Group
                    key={field.name}
                    align="flex-end"
                    gap="xs"
                    wrap="nowrap"
                    data-testid={field.testId}
                    data-value={timestamps[field.name] ?? ""}
                >
                    <DateTimePicker
                        style={{flex: 1}}
                        label={t(`songs:tools.changeTimestamps.fields.${field.name}`)}
                        value={toPickerValue(timestamps[field.name])}
                        onChange={value => setTimestamp(field.name, fromPickerValue(value))}
                        valueFormat="YYYY-MM-DD HH:mm:ss"
                        withSeconds
                        required={field.required}
                        clearable={!field.required}
                        popoverProps={{zIndex: ZINDEX_LIGHTBOX}}
                    />
                    <Button
                        variant="light"
                        onClick={() => setTimestamp(field.name, new Date().toISOString())}
                    >
                        {t("songs:tools.changeTimestamps.setNow")}
                    </Button>
                </Group>
            ))}
            <Group justify="flex-end" gap="xs">
                <Button variant="subtle" onClick={onClose}>
                    {t("common:actions.cancel")}
                </Button>
                <Button onClick={handleSave} disabled={!canSave} loading={isPending}>
                    {t("common:actions.save")}
                </Button>
            </Group>
        </Stack>
    );
}
