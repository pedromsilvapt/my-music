import {Box, Button, Group, Skeleton, Stack, Text} from "@mantine/core";
import type {ContextModalProps} from "@mantine/modals";
import {notifications} from "@mantine/notifications";
import {IconArrowMerge} from "@tabler/icons-react";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useResolveSoundalikes} from "../../hooks/useResolveSoundalikes.ts";
import {useSoundalikeMatch} from "../../hooks/useSoundalikeMatch.ts";
import {SecondaryAction} from "../../model/secondaryAction.ts";
import SoundalikeGroupCard from "../audits/soundalike-group-card.tsx";
import SoundalikeResolutionSummary from "../audits/soundalike-resolution-summary.tsx";
import {type GroupSelection, selectPrimary, setAction, toSecondaryActions} from "../audits/soundalike-selection.ts";

export interface SongMergeModalInnerProps {
    songIds: number[];
}

/**
 * Merges songs picked by the user, the same way a group of detected soundalikes is resolved: one song is kept, and
 * the others are merged into it. Their match score is shown for information only, however low it is.
 */
export default function SongMergeModal({context, id, innerProps}: ContextModalProps<SongMergeModalInnerProps>) {
    const {t} = useTranslation(["songs", "audits", "common"]);
    const matchQuery = useSoundalikeMatch(innerProps.songIds);
    const resolveMutation = useResolveSoundalikes();
    const [selection, setSelection] = useState<GroupSelection | undefined>(undefined);

    const match = useQueryData(matchQuery, t("songs:merge.fetchFailed"))?.data;

    if (!match?.songs) {
        return (
            <Box data-testid="song-merge" data-loading="true">
                <Skeleton height={160}/>
            </Box>
        );
    }

    const songs = match.songs;

    const handleMerge = () => {
        if (selection?.primaryId == null) return;

        resolveMutation.mutate(
            {
                data: {
                    resolutions: [{
                        nonConformityId: null,
                        primarySongId: selection.primaryId,
                        secondaryActions: toSecondaryActions(selection),
                    }],
                },
            },
            {
                onSuccess: () => {
                    notifications.show({
                        title: t("common:status.success"),
                        message: t("songs:merge.success"),
                        color: "green",
                    });
                    context.closeModal(id);
                },
                onError: (error: unknown) => {
                    notifications.show({
                        title: t("common:status.error"),
                        message: t("songs:merge.failed", {error: `${error}`}),
                        color: "red",
                    });
                },
            },
        );
    };

    return (
        <Stack data-testid="song-merge" data-loading={matchQuery.isFetching ? "true" : "false"}>
            <Text c="dimmed" size="sm">
                {t("songs:merge.instructions")}
            </Text>

            <SoundalikeGroupCard
                songs={songs}
                matchScore={match.matchScore}
                selection={selection}
                onSelectPrimary={(songId) => setSelection(prev =>
                    selectPrimary(prev, songs.map(s => s.id), songId, SecondaryAction.Merge) ?? undefined)}
                onSetAction={(songId, action) => setSelection(prev => prev && setAction(prev, songId, action))}
            />

            {selection && <SoundalikeResolutionSummary songs={songs} selection={selection}/>}

            <Group justify="flex-end">
                <Button variant="default" onClick={() => context.closeModal(id)}>
                    {t("common:actions.cancel")}
                </Button>
                <Button
                    leftSection={<IconArrowMerge size={16}/>}
                    onClick={handleMerge}
                    disabled={selection?.primaryId == null}
                    loading={resolveMutation.isPending}
                    data-testid="song-merge-confirm"
                >
                    {t("songs:merge.confirm")}
                </Button>
            </Group>
        </Stack>
    );
}
