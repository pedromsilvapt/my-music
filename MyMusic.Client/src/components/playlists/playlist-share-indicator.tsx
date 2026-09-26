import {Tooltip} from "@mantine/core";
import {IconShare, IconUsers} from "@tabler/icons-react";
import {useTranslation} from "react-i18next";

export interface PlaylistShareInfo {
    isSharedWithMe: boolean;
    ownerName: string;
    sharedWithCount: number;
}

interface PlaylistShareIndicatorProps {
    playlist: PlaylistShareInfo;
    size?: number;
}

/**
 * Marks a shared playlist: owners see how many users it is shared with, recipients see who shared it.
 * Renders nothing for playlists that are not shared.
 */
export default function PlaylistShareIndicator({playlist, size = 16}: PlaylistShareIndicatorProps) {
    const {t} = useTranslation(["playlists"]);

    if (playlist.isSharedWithMe) {
        return (
            <Tooltip label={t("playlists:share.sharedBy", {name: playlist.ownerName})} openDelay={300}>
                <IconShare
                    data-testid="playlist-share-indicator"
                    data-share-state="shared-with-me"
                    aria-label={t("playlists:share.sharedBy", {name: playlist.ownerName})}
                    size={size}
                    color="var(--mantine-color-blue-5)"
                    style={{flexShrink: 0}}
                />
            </Tooltip>
        );
    }

    if (playlist.sharedWithCount > 0) {
        return (
            <Tooltip label={t("playlists:share.sharedWith", {count: playlist.sharedWithCount})} openDelay={300}>
                <IconUsers
                    data-testid="playlist-share-indicator"
                    data-share-state="shared-by-me"
                    aria-label={t("playlists:share.sharedWith", {count: playlist.sharedWithCount})}
                    size={size}
                    color="var(--mantine-color-green-6)"
                    style={{flexShrink: 0}}
                />
            </Tooltip>
        );
    }

    return null;
}
