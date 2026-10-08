import {IconCheck} from "@tabler/icons-react";
import {useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useManualFetchMetadata} from "../../hooks/useManualFetchMetadata";
import type {SongMetadataDiff} from "../../model/songMetadataDiff";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import SourceSongSearchModal from "./source-song-search-modal.tsx";
import type {MetadataSearchItem} from "./use-metadata-search-schema";

interface MetadataSearchModalProps {
    opened: boolean;
    onClose: () => void;
    song: GetSongResponseSong;
    onSelect: (metadata: SongMetadataDiff) => void;
}

export default function MetadataSearchModal({
    opened,
    onClose,
    song,
    onSelect,
}: MetadataSearchModalProps) {
    const {t} = useTranslation(["songs", "common"]);
    const {manualFetch} = useManualFetchMetadata(onSelect);

    const handleApply = useCallback(async (item: MetadataSearchItem) => {
        await manualFetch(song.id, item);
    }, [song.id, manualFetch]);

    const action = useMemo(() => ({
        icon: <IconCheck size={16}/>,
        label: t("songs:metadataSearch.apply"),
    }), [t]);

    return (
        <SourceSongSearchModal
            opened={opened}
            onClose={onClose}
            song={song}
            title={t("songs:metadataSearch.title")}
            searchPlaceholder={t("songs:metadataSearch.searchPlaceholder")}
            testId="metadata-search"
            action={action}
            onPick={handleApply}
        />
    );
}
