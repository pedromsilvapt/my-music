import {useCallback, useEffect, useState} from "react";
import {
    useBatchDeleteAuditNonConformities,
    useBatchSetAuditWaiver,
} from "../../client/audits.ts";
import Collection from "../common/collection/collection.tsx";
import GrantWaiverModal from "./grant-waiver-modal.tsx";
import {useAuditNonConformitiesSchema} from "./useAuditNonConformitiesSchema.tsx";
import {notifications} from "@mantine/notifications";
import type {ListAuditNonConformityItem} from "../../model";
import type {ListAuditNonConformityItemWithSong} from "./useAuditNonConformitiesSchema.tsx";

interface AuditDefaultComponentProps {
    nonConformities: ListAuditNonConformityItem[];
    ruleId: number;
    refetchNonConformities: () => void;
    serverSearch?: string;
    serverFilter?: string;
    onServerFilterChange?: (search: string, filter: string) => void;
}

export default function AuditDefaultComponent({
    nonConformities,
    ruleId,
    refetchNonConformities,
    serverSearch,
    serverFilter,
    onServerFilterChange,
}: AuditDefaultComponentProps) {
    const batchSetWaiverMutation = useBatchSetAuditWaiver();
    const batchDeleteMutation = useBatchDeleteAuditNonConformities();

    const [waiverModalOpen, setWaiverModalOpen] = useState(false);
    const [pendingWaiverIds, setPendingWaiverIds] = useState<number[]>([]);

    const nonConformitiesWithSongs: ListAuditNonConformityItemWithSong[] = [];
    
    for (const nc of nonConformities) {
        if (nc.song != null) {
            nonConformitiesWithSongs.push(nc as ListAuditNonConformityItemWithSong);
        }
    }

    const skippedCount = nonConformities.length - nonConformitiesWithSongs.length;
    if (skippedCount > 0) {
        notifications.show({
            title: "Warning",
            message: `${skippedCount} non-conformity(ies) without associated songs were skipped`,
            color: "yellow"
        });
    }

    useEffect(() => {
        refetchNonConformities();
    }, [refetchNonConformities]);

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

    const schema = useAuditNonConformitiesSchema(ruleId, handleSetWaiver, handleDelete);

    return (
        <div style={{height: '100%', display: 'flex', flexDirection: 'column'}}>
            <div style={{flex: 1, minHeight: 0}}>
                <Collection
                    stateKey="audit-detail"
                    items={nonConformitiesWithSongs}
                    schema={schema}
                    filterMode="server"
                    serverSearch={serverSearch}
                    serverFilter={serverFilter}
                    onServerFilterChange={onServerFilterChange}
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
        </div>
    );
}
