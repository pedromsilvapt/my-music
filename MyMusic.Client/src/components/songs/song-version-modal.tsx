import {ActionIcon, Box, Group, ScrollArea, Stack, Text} from "@mantine/core";
import {IconChevronLeft, IconChevronRight, IconMusic} from "@tabler/icons-react";
import type {ContextModalProps} from "@mantine/modals";
import {useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import type {GetSongHistoryItem, GetSongResponseSong} from "../../model";
import {useGetSongHistoryDiff} from "../../client/song-history.ts";
import {formatRelativeDate} from "../../utils/format-relative-date.ts";
import Artwork from "../common/artwork.tsx";

interface DiffField<T> {
    old?: T | null;
    new?: T | null;
}

export interface SongVersionModalInnerProps {
    historyItems: GetSongHistoryItem[];
    currentSong: GetSongResponseSong;
    initialIndex: number;
    firstRevisionId: number | null;
}

const PRE_STYLE_OLD: React.CSSProperties = {
    margin: 0,
    padding: "12px",
    background: "var(--mantine-color-red-0)",
    border: "1px solid var(--mantine-color-red-6)",
    color: "var(--mantine-color-gray-7)",
    borderRadius: "6px",
    fontFamily: "var(--mantine-font-family-monospace)",
    fontSize: "13px",
    lineHeight: 1.5,
    whiteSpace: "pre-wrap",
    wordBreak: "break-word",
};

const PRE_STYLE_NEW: React.CSSProperties = {
    margin: 0,
    padding: "12px",
    background: "var(--mantine-color-green-0)",
    border: "1px solid var(--mantine-color-green-6)",
    color: "var(--mantine-color-gray-7)",
    borderRadius: "6px",
    fontFamily: "var(--mantine-font-family-monospace)",
    fontSize: "13px",
    lineHeight: 1.5,
    whiteSpace: "pre-wrap",
    wordBreak: "break-word",
};

function parseCoverId(diff: unknown, side: "old" | "new"): number | null {
    if (diff == null || typeof diff !== "object") return null;
    const delta = diff as Record<string, unknown>;

    const coverIdField = delta.cover_id as DiffField<number | null> | undefined;
    if (coverIdField != null && typeof coverIdField === "object" && side in coverIdField) {
        const val = coverIdField[side];
        return typeof val === "number" ? val : null;
    }

    const coverField = delta.cover as DiffField<{ id?: number } | null> | undefined;
    if (coverField != null && typeof coverField === "object" && side in coverField) {
        const cover = coverField[side];
        if (cover != null && typeof cover === "object" && "id" in cover) {
            const id = (cover as { id?: number }).id;
            return typeof id === "number" ? id : null;
        }
    }

    return null;
}

export default function SongVersionModal({
    innerProps,
}: ContextModalProps<SongVersionModalInnerProps>) {
    const {t} = useTranslation(["songs", "player"]);
    const {historyItems, currentSong, initialIndex, firstRevisionId} = innerProps;

    const [currentIndex, setCurrentIndex] = useState(initialIndex);
    const currentItem = historyItems[currentIndex];

    const isFirstRevision = currentItem.id === firstRevisionId;

    const diffQuery = useGetSongHistoryDiff(
        currentSong.id,
        currentItem.id,
    );

    const newCoverId = useMemo(() => parseCoverId(currentItem.diff, "new"), [currentItem.diff]);
    const oldCoverId = useMemo(
        () => !isFirstRevision ? parseCoverId(currentItem.diff, "old") : null,
        [currentItem.diff, isFirstRevision],
    );

    const hasCoverChange = diffQuery.data?.data?.metadata?.cover != null;

    const displayMetadata = useMemo(() => {
        const metadata = diffQuery.data?.data?.metadata;
        if (!metadata) return null;
        return metadata as Record<string, unknown>;
    }, [diffQuery.data]);

    const relativeDate = formatRelativeDate(currentItem.createdAt);

    const hasPrevious = currentIndex < historyItems.length - 1;
    const hasNext = currentIndex > 0;

    return (
        <Stack gap="md" data-testid="song-version-modal" data-loading={diffQuery.isFetching ? "true" : "false"}>
            <Group gap="xs" align="center">
                <ActionIcon
                    variant="light"
                    onClick={() => setCurrentIndex(i => Math.min(i + 1, historyItems.length - 1))}
                    disabled={!hasPrevious}
                    data-testid="song-version-previous"
                    aria-label={t("player:actions.previous")}
                >
                    <IconChevronLeft/>
                </ActionIcon>
                <Text size="md" fw={600} data-testid="song-version-header">
                    {t("songs:detailPage.versionHeader", {
                        revision: currentItem.songRevision,
                        relativeDate,
                    })}
                </Text>
                <ActionIcon
                    variant="light"
                    onClick={() => setCurrentIndex(i => Math.max(i - 1, 0))}
                    disabled={!hasNext}
                    data-testid="song-version-next"
                    aria-label={t("player:actions.next")}
                >
                    <IconChevronRight/>
                </ActionIcon>
            </Group>

            {hasCoverChange && (
                <Group gap="lg" align="flex-start">
                    {!isFirstRevision && (
                        <Box data-testid="song-version-old-cover">
                            <Text size="xs" c="dimmed" mb={4}>
                                {t("songs:detailPage.versionOldLabel")}
                            </Text>
                            <Artwork
                                id={oldCoverId}
                                size={80}
                                placeholderIcon={<IconMusic size={32}/>}
                                enablePreview={false}
                            />
                        </Box>
                    )}
                    <Box data-testid="song-version-new-cover">
                        <Text size="xs" c="dimmed" mb={4}>
                            {t("songs:detailPage.versionNewLabel")}
                        </Text>
                        <Artwork
                            id={newCoverId}
                            size={80}
                            placeholderIcon={<IconMusic size={32}/>}
                            enablePreview={false}
                        />
                    </Box>
                </Group>
            )}

            <Group gap="md" align="flex-start" grow>
                {!isFirstRevision && (
                    <Stack gap={0} style={{flex: 1}}>
                        <Text size="sm" fw={600} mb={4}>
                            {t("songs:detailPage.versionOldLabel")}
                        </Text>
                        <ScrollArea h={400} data-testid="song-version-old">
                            <Box component="pre" style={PRE_STYLE_OLD}>
                                {displayMetadata
                                    ? JSON.stringify(extractSide(displayMetadata, "old"), null, 2)
                                    : ""}
                            </Box>
                        </ScrollArea>
                    </Stack>
                )}
                <Stack gap={0} style={{flex: 1}}>
                    <Text size="sm" fw={600} mb={4}>
                        {t("songs:detailPage.versionNewLabel")}
                    </Text>
                    <ScrollArea h={400} data-testid="song-version-new">
                        <Box component="pre" style={PRE_STYLE_NEW}>
                            {displayMetadata
                                ? JSON.stringify(extractSide(displayMetadata, "new"), null, 2)
                                : ""}
                        </Box>
                    </ScrollArea>
                </Stack>
            </Group>
        </Stack>
    );
}

function extractSide(metadata: Record<string, unknown>, side: "old" | "new"): Record<string, unknown> {
    const result: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(metadata)) {
        if (value == null || typeof value !== "object") continue;
        const field = value as DiffField<unknown>;
        if (!("old" in field) && !("new" in field)) continue;
        if (field.old === field.new) continue;
        result[key] = side === "old" ? field.old ?? null : field.new ?? null;
    }
    return result;
}