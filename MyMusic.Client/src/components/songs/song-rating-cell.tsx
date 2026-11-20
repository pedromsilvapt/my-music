import {Button, Group, Popover, Rating, Stack, Text, UnstyledButton} from "@mantine/core";
import {useDisclosure} from "@mantine/hooks";
import type {SyntheticEvent} from "react";
import {useTranslation} from "react-i18next";

interface SongRatingCellProps {
    songId: number;
    rating: number | null | undefined;
    onChange: (songId: number, rating: number | null) => void;
}

// The dropdown is portalled, but React events still bubble to the row: keep them from (de)selecting or playing it
const stopPropagation = (ev: SyntheticEvent) => ev.stopPropagation();

/** Compact `3.5 ★` rating that opens a star picker to change the rating of the song in place. */
export function SongRatingCell({songId, rating, onChange}: SongRatingCellProps) {
    const {t} = useTranslation(["songs"]);
    const [opened, {toggle, close}] = useDisclosure(false);

    const handleChange = (value: number | null) => {
        close();
        if (value !== (rating ?? null)) {
            onChange(songId, value);
        }
    };

    return (
        <Popover opened={opened} onChange={open => !open && close()} position="bottom" withArrow shadow="md" trapFocus>
            <Popover.Target>
                <UnstyledButton
                    data-testid={`song-rating-${songId}`}
                    aria-label={t("songs:schema.columns.rating")}
                    onClick={toggle}
                    onDoubleClick={stopPropagation}
                >
                    <Text size="sm" c={rating ? undefined : "dimmed"} style={{whiteSpace: "nowrap", fontVariantNumeric: "tabular-nums"}}>
                        {rating ? `${rating} ★` : "– ☆"}
                    </Text>
                </UnstyledButton>
            </Popover.Target>
            <Popover.Dropdown onClick={stopPropagation} onDoubleClick={stopPropagation} onContextMenu={stopPropagation}>
                <Stack gap="xs" align="center" data-testid={`song-rating-picker-${songId}`}>
                    <Rating fractions={2} value={rating ?? 0} onChange={handleChange}/>
                    <Group justify="center">
                        <Button size="compact-xs" variant="subtle" disabled={rating == null} onClick={() => handleChange(null)}>
                            {t("songs:schema.clearRating")}
                        </Button>
                    </Group>
                </Stack>
            </Popover.Dropdown>
        </Popover>
    );
}
