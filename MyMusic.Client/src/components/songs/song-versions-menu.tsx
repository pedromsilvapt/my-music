import {Anchor, Menu, Text} from "@mantine/core";
import {modals} from "@mantine/modals";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
import type {GetSongHistoryItem, GetSongResponseSong} from "../../model";
import {formatRelativeDate} from "../../utils/format-relative-date.ts";
import {SONG_VERSION_MODAL_SIZE} from "../../consts.ts";

interface SongVersionsMenuProps {
    song: GetSongResponseSong;
    historyItems: GetSongHistoryItem[];
    firstRevisionId: number | null;
}

export default function SongVersionsMenu({song, historyItems, firstRevisionId}: SongVersionsMenuProps) {
    const {t} = useTranslation(["songs", "common"]);

    const handleVersionClick = useCallback((index: number) => {
        modals.openContextModal({
            modal: "song-version",
            title: t("songs:detailPage.versionDiffTitle"),
            size: SONG_VERSION_MODAL_SIZE,
            closeButtonProps: {"aria-label": t("common:actions.close")},
            innerProps: {
                historyItems,
                currentSong: song,
                initialIndex: index,
                firstRevisionId,
            },
        });
    }, [song, historyItems, firstRevisionId, t]);

    const versionsCount = historyItems.length;

    if (versionsCount === 0) return null;

    return (
        <Menu shadow="md" width={260} position="bottom-start" withinPortal>
            <Menu.Target>
                <Anchor component="button" size="sm" c="blue" data-testid="song-versions-trigger">
                    {t("songs:detailPage.versions", {count: versionsCount})}
                </Anchor>
            </Menu.Target>
            <Menu.Dropdown>
                {historyItems.map((item, index) => (
                    <Menu.Item
                        key={item.id}
                        data-testid="song-version-item"
                        data-history-id={item.id}
                        data-revision={item.songRevision}
                        onClick={() => handleVersionClick(index)}
                    >
                        <Text size="sm">
                            {t("songs:detailPage.versionItemShort", {
                                revision: item.songRevision,
                                relativeDate: formatRelativeDate(item.createdAt),
                            })}
                        </Text>
                    </Menu.Item>
                ))}
            </Menu.Dropdown>
        </Menu>
    );
}