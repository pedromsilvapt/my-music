import {Button, Tooltip} from "@mantine/core";
import {useTranslation} from "react-i18next";

interface SearchLyricsToggleProps {
    checked: boolean;
    onChange: (checked: boolean) => void;
}

/**
 * Pill shown inside the songs search input; when on, the search text also matches lyrics.
 */
export default function SearchLyricsToggle({checked, onChange}: SearchLyricsToggleProps) {
    const {t} = useTranslation(["common"]);

    return (
        <Tooltip label={t("common:songs.searchLyricsTooltip")}>
            <Button
                size="compact-xs"
                radius="xl"
                variant={checked ? "filled" : "default"}
                aria-pressed={checked}
                data-testid="search-lyrics-toggle"
                onClick={() => onChange(!checked)}
            >
                {t("common:songs.searchLyrics")}
            </Button>
        </Tooltip>
    );
}
