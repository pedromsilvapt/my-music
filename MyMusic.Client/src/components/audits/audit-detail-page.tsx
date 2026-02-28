import {Badge, Button, Group, Text, Title} from "@mantine/core";
import {IconRefresh} from '@tabler/icons-react';
import {useQueryClient} from "@tanstack/react-query";
import {useParams} from "@tanstack/react-router";
import {useCallback, useEffect, useState} from "react";
import {
    useBatchDeleteAuditNonConformities,
    useBatchSetAuditWaiver,
    useGetAuditRule,
    useListAuditNonConformities,
    useScanAuditRule
} from "../../client/audits.ts";
import {getSong} from "../../client/songs.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {GetSongResponseSong} from "../../model";
import Collection from "../common/collection/collection.tsx";
import SongEditorModal from "../songs/song-editor-modal.tsx";
import GrantWaiverModal from "./grant-waiver-modal.tsx";
import {useAuditNonConformitiesSchema} from "./useAuditNonConformitiesSchema.tsx";

export default function AuditDetailPage() {
    const {auditId} = useParams({from: '/audits/$auditId'});
    const id = parseInt(auditId, 10);
    const queryClient = useQueryClient();

    const ruleQuery = useGetAuditRule(id);
    const nonConformitiesQuery = useListAuditNonConformities(id);

    const ruleResponse = useQueryData(ruleQuery, "Failed to fetch audit rule");
    const nonConformitiesResponse = useQueryData(
        nonConformitiesQuery,
        "Failed to fetch non conformities"
    ) ?? {data: {nonConformities: [], total: 0}};

    const refetchNonConformities = nonConformitiesQuery.refetch;

    const scanMutation = useScanAuditRule();
    const batchSetWaiverMutation = useBatchSetAuditWaiver();
    const batchDeleteMutation = useBatchDeleteAuditNonConformities();

    const [scanning, setScanning] = useState(false);
    const [waiverModalOpen, setWaiverModalOpen] = useState(false);
    const [pendingWaiverIds, setPendingWaiverIds] = useState<number[]>([]);
    const [editorOpened, setEditorOpened] = useState(false);
    const [songsToEdit, setSongsToEdit] = useState<GetSongResponseSong[]>([]);

    const rule = ruleResponse?.data?.rule;
    const nonConformities = nonConformitiesResponse?.data?.nonConformities ?? [];

    useEffect(() => {
        void refetchNonConformities();
    }, [refetchNonConformities]);

    const handleScan = useCallback(async () => {
        setScanning(true);
        try {
            await scanMutation.mutateAsync({id});
            await refetchNonConformities();
        } finally {
            setScanning(false);
        }
    }, [id, scanMutation, refetchNonConformities]);

    const handleSetWaiver = useCallback(async (ids: number[], hasWaiver: boolean, _reason?: string | null) => {
        if (hasWaiver) {
            setPendingWaiverIds(ids);
            setWaiverModalOpen(true);
        } else {
            await batchSetWaiverMutation.mutateAsync({
                data: {
                    ids,
                    hasWaiver: false,
                    waiverReason: null
                }
            });
            await refetchNonConformities();
        }
    }, [batchSetWaiverMutation, refetchNonConformities]);

    const handleWaiverConfirm = useCallback(async (reason: string | null) => {
        await batchSetWaiverMutation.mutateAsync({
            data: {
                ids: pendingWaiverIds,
                hasWaiver: true,
                waiverReason: reason
            }
        });
        setWaiverModalOpen(false);
        setPendingWaiverIds([]);
        await refetchNonConformities();
    }, [batchSetWaiverMutation, pendingWaiverIds, refetchNonConformities]);

    const handleDelete = useCallback(async (ids: number[]) => {
        await batchDeleteMutation.mutateAsync({
            data: {ids}
        });
        await refetchNonConformities();
    }, [batchDeleteMutation, refetchNonConformities]);

    const handleEditSongs = useCallback(async (songIds: number[]) => {
        const songs: GetSongResponseSong[] = [];
        for (const songId of songIds) {
            const response = await getSong(songId);
            if (response.data.song) {
                songs.push(response.data.song);
            }
        }
        setSongsToEdit(songs);
        setEditorOpened(true);
    }, []);

    const handleEditorSuccess = useCallback(async () => {
        setEditorOpened(false);
        setSongsToEdit([]);
        await queryClient.invalidateQueries({queryKey: ["api", "audits"]});
    }, [queryClient]);

    const schema = useAuditNonConformitiesSchema(handleSetWaiver, handleDelete, handleEditSongs);

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <Group justify="space-between" mb="md">
                <Group>
                    <Title order={2}>{rule?.name ?? 'Audit Rule'}</Title>
                    <Badge color={rule?.nonConformityCount ? 'red' : 'green'}>
                        {rule?.nonConformityCount ?? 0} issues
                    </Badge>
                </Group>
                <Button
                    leftSection={<IconRefresh size={16}/>}
                    onClick={handleScan}
                    loading={scanning}
                >
                    Run Scan
                </Button>
            </Group>

            <Text c="dimmed" mb="md">{rule?.description}</Text>

            <div style={{flex: 1, minHeight: 0}}>
                <Collection
                    stateKey="audit-detail"
                    items={nonConformities}
                    schema={schema}
                    filterMode="client"
                    searchPlaceholder="Search non-conformities..."
                />
            </div>

            <GrantWaiverModal
                opened={waiverModalOpen}
                onClose={() => {
                    setWaiverModalOpen(false);
                    setPendingWaiverIds([]);
                }}
                onConfirm={handleWaiverConfirm}
                count={pendingWaiverIds.length}
                loading={batchSetWaiverMutation.isPending}
            />

            <SongEditorModal
                opened={editorOpened}
                onClose={() => {
                    setEditorOpened(false);
                    setSongsToEdit([]);
                }}
                songs={songsToEdit}
                onSuccess={handleEditorSuccess}
            />
        </div>
    );
}
