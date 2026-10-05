import {Button, Group, Stack, Text, Modal, Divider} from "@mantine/core";
import {IconTrash} from '@tabler/icons-react';
import {useGetSoundalikeDuplicates, useUpdateSoundalikeSelection} from "../../client/audits.ts";
import {useResolveSoundalikes} from "../../hooks/useResolveSoundalikes.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useCallback, useEffect, useState} from "react";
import {useTranslation} from "react-i18next";
import type {SoundalikeDuplicateGroup} from "../../model/soundalikeDuplicateGroup.ts";
import type {GetSoundalikeDuplicatesResponse} from "../../model/getSoundalikeDuplicatesResponse.ts";
import {SecondaryAction} from "../../model/secondaryAction.ts";
import {notifications} from "@mantine/notifications";
import SoundalikeToolbar from "./soundalike-toolbar.tsx";
import SoundalikeGroupCard from "./soundalike-group-card.tsx";
import SoundalikeResolutionSummary from "./soundalike-resolution-summary.tsx";
import {type GroupSelection, selectPrimary, setAction, type SongAction, toSecondaryActions} from "./soundalike-selection.ts";

interface SoundalikePageProps {
    onToolbarChange: (toolbar: React.ReactNode) => void;
}

function groupSelectionFromServer(group: SoundalikeDuplicateGroup): GroupSelection | undefined {
    if (group.primarySongId == null) return undefined;

    const actions = new Map<number, SongAction>();
    if (group.secondaryActions) {
        for (const [k, v] of Object.entries(group.secondaryActions)) {
            actions.set(Number(k), v as SongAction);
        }
    }

    return {
        primaryId: group.primarySongId,
        actions
    };
}

function toSelectionRequest(selection: GroupSelection) {
    const secondaryActions: Record<string, SongAction> = {};
    for (const [k, v] of selection.actions.entries()) {
        secondaryActions[k] = v;
    }
    return {
        primarySongId: selection.primaryId,
        secondaryActions
    };
}

export default function SoundalikePage({onToolbarChange}: SoundalikePageProps) {
    const {t} = useTranslation(["audits", "common"]);
    const soundalikesQuery = useGetSoundalikeDuplicates();
    const resolveMutation = useResolveSoundalikes();
    const selectionMutation = useUpdateSoundalikeSelection();
    const [selectedGroups, setSelectedGroups] = useState<Map<number, GroupSelection>>(new Map());
    // The groups awaiting confirmation: all the selected ones, or the single group being resolved on its own
    const [confirmGroupIds, setConfirmGroupIds] = useState<number[] | null>(null);

    const soundalikesResponse = useQueryData(
        soundalikesQuery,
        t("audits:soundalike.fetchFailed")
    );

    const groups: SoundalikeDuplicateGroup[] = (soundalikesResponse as { data: GetSoundalikeDuplicatesResponse } | null)?.data?.groups ?? [];

    useEffect(() => {
        const serverGroups = (soundalikesResponse as { data: GetSoundalikeDuplicatesResponse } | null)?.data?.groups;
        if (!serverGroups || serverGroups.length === 0) return;

        const initialMap = new Map<number, GroupSelection>();
        for (const group of serverGroups) {
            const selection = groupSelectionFromServer(group);
            if (selection) {
                initialMap.set(group.nonConformityId, selection);
            }
        }
        setSelectedGroups(initialMap);
    }, [soundalikesResponse]);

    const persistSelection = useCallback((nonConformityId: number, selection: GroupSelection) => {
        selectionMutation.mutate({
            nonConformityId,
            data: toSelectionRequest(selection)
        });
    }, [selectionMutation]);

    const handleSelectPrimary = (nonConformityId: number, songId: number) => {
        setSelectedGroups(prev => {
            const newMap = new Map(prev);
            const songIds = groups
                .find((g: SoundalikeDuplicateGroup) => g.nonConformityId === nonConformityId)
                ?.songs.map((s) => s.id) ?? [];
            const newSelection = selectPrimary(newMap.get(nonConformityId), songIds, songId, SecondaryAction.Delete);
            if (newSelection) {
                newMap.set(nonConformityId, newSelection);
            } else {
                newMap.delete(nonConformityId);
            }
            persistSelection(nonConformityId, newSelection ?? {primaryId: null, actions: new Map()});
            return newMap;
        });
    };

    const handleSetAction = (nonConformityId: number, songId: number, action: SongAction) => {
        setSelectedGroups(prev => {
            const newMap = new Map(prev);
            const existing = newMap.get(nonConformityId);
            if (existing) {
                const newSelection = setAction(existing, songId, action);
                newMap.set(nonConformityId, newSelection);
                persistSelection(nonConformityId, newSelection);
            }
            return newMap;
        });
    };

    const confirmSelections = Array.from(selectedGroups.entries())
        .filter(([nonConformityId, selection]) => selection.primaryId != null && confirmGroupIds?.includes(nonConformityId));

    const handleResolve = async () => {
        setConfirmGroupIds(null);

        const resolutions = confirmSelections
            .map(([nonConformityId, selection]) => ({
            nonConformityId,
            primarySongId: selection.primaryId!,
            secondaryActions: toSecondaryActions(selection),
        }));

        await resolveMutation.mutateAsync(
            {data: {resolutions}},
            {
                onSuccess: () => {
                    notifications.show({
                        title: t("common:status.success"),
                        message: t("audits:soundalike.resolveSuccess", {count: resolutions.length}),
                        color: "green"
                    });
                    setSelectedGroups(prev => {
                        const newMap = new Map(prev);
                        for (const resolution of resolutions) {
                            newMap.delete(resolution.nonConformityId);
                        }
                        return newMap;
                    });
                    soundalikesQuery.refetch();
                },
                onError: (error: unknown) => {
                    notifications.show({
                        title: t("common:status.error"),
                        message: t("audits:soundalike.resolveFailed", {error: `${error}`}),
                        color: "red"
                    });
                }
            }
        );
    };

    const getTotalSongsToDelete = () => {
        return confirmSelections.reduce((sum, [, sel]) => {
            return sum + Array.from(sel.actions.values())
                .filter(a => a === SecondaryAction.Delete || a === SecondaryAction.Merge)
                .length;
        }, 0);
    };

    const getTotalSongsToMerge = () => {
        return confirmSelections.reduce((sum, [, sel]) => {
            return sum + Array.from(sel.actions.values())
                .filter(a => a === SecondaryAction.Merge)
                .length;
        }, 0);
    };

    const readyToResolve = selectedGroups.size > 0;

    useEffect(() => {
        onToolbarChange(
            <SoundalikeToolbar
                selectedGroupsCount={selectedGroups.size}
                readyToResolve={readyToResolve}
                onRemoveDuplicates={() => setConfirmGroupIds(Array.from(selectedGroups.keys()))}
            />
        );
    }, [onToolbarChange, selectedGroups, readyToResolve]);

    return (
        <div style={{height: '100%', display: 'flex', flexDirection: 'column'}} data-testid="soundalikes"
             data-loading={soundalikesQuery.isFetching ? "true" : "false"}>
            <Text c="dimmed" mb="md">
                {t("audits:soundalike.instructions")}
            </Text>

            <div style={{flex: 1, overflow: 'auto'}}>
                <Stack gap="md">
                    {groups.map((group: SoundalikeDuplicateGroup) => (
                        <SoundalikeGroupCard
                            key={group.nonConformityId}
                            songs={group.songs}
                            matchScore={group.matchScore}
                            selection={selectedGroups.get(group.nonConformityId)}
                            onSelectPrimary={(songId) => handleSelectPrimary(group.nonConformityId, songId)}
                            onSetAction={(songId, action) => handleSetAction(group.nonConformityId, songId, action)}
                            onResolve={() => setConfirmGroupIds([group.nonConformityId])}
                        />
                    ))}

                    {groups.length === 0 && (
                        <Text c="dimmed" ta="center" mt="xl">
                            {t("audits:soundalike.noDuplicates")}
                        </Text>
                    )}
                </Stack>
            </div>

            <Modal
                opened={confirmGroupIds != null}
                onClose={() => setConfirmGroupIds(null)}
                title={t("audits:soundalike.confirmTitle")}
                size="lg"
            >
                <Stack>
                    <Text>
                        {t("audits:soundalike.confirmProcessLabel")} <strong>{t("audits:soundalike.groupCount", {count: confirmSelections.length})}</strong>:
                    </Text>
                    <Group gap="md">
                        <Text size="sm"><strong>{getTotalSongsToDelete()}</strong> {t("audits:soundalike.confirmSongsToDelete", {count: getTotalSongsToDelete()})}</Text>
                        {getTotalSongsToMerge() > 0 && (
                            <Text size="sm"><strong>{getTotalSongsToMerge()}</strong> {t("audits:soundalike.confirmSongsToMerge", {count: getTotalSongsToMerge()})}</Text>
                        )}
                    </Group>

                    <Divider my="sm" />

                    {confirmSelections.map(([groupId, selection]) => {
                        const group = groups.find(g => g.nonConformityId === groupId);
                        if (!group) return null;

                        return <SoundalikeResolutionSummary key={groupId} songs={group.songs} selection={selection}/>;
                    })}

                    <Divider my="sm" />

                    <Group justify="flex-end">
                        <Button variant="default" onClick={() => setConfirmGroupIds(null)}>
                            {t("common:actions.cancel")}
                        </Button>
                        <Button
                            color="red"
                            leftSection={<IconTrash size={16}/>}
                            onClick={handleResolve}
                            loading={resolveMutation.isPending}
                        >
                            {t("audits:soundalike.resolveButton", {count: confirmSelections.length})}
                        </Button>
                    </Group>
                </Stack>
            </Modal>
        </div>
    );
}
