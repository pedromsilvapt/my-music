import {Badge, Button, Group, Text, Title} from "@mantine/core";
import {IconRefresh} from '@tabler/icons-react';
import {useParams} from "@tanstack/react-router";
import {useCallback, useEffect, useState} from "react";
import {
    useGetAuditRule,
    useListAuditNonConformities,
    useScanAuditRule
} from "../../client/audits.ts";
import {useCollectionActions, useCollectionStateByKey} from "../../stores/collection-store.tsx";
import {useQueryData} from "../../hooks/use-query-data.ts";
import AuditDefaultComponent from "./audit-default-component.tsx";
import SoundalikePage from "./soundalike-page.tsx";

const AUDIT_CUSTOM_COMPONENTS: Record<string, React.ComponentType<{onToolbarChange: (toolbar: React.ReactNode) => void}>> = {
    'soundalike': SoundalikePage,
};

const AUDIT_STATE_KEY = "audit-detail";

export default function AuditDetailPage() {
    const {auditId} = useParams({from: '/audits/$auditId'});
    const id = parseInt(auditId, 10);

    const {setCollectionFilter} = useCollectionActions(state => ({
        setCollectionFilter: state.setCollectionFilter,
    }));
    const collectionState = useCollectionStateByKey(AUDIT_STATE_KEY);
    const appliedSearch = collectionState.filter.search;
    const appliedFilter = collectionState.filter.expression;

    const ruleQuery = useGetAuditRule(id);
    const nonConformitiesQuery = useListAuditNonConformities(
        id,
        { search: appliedSearch, filter: appliedFilter },
        {
            query: {
                enabled: true,
                select: (response) => response.data,
            }
        }
    );

    const ruleResponse = useQueryData(ruleQuery, "Failed to fetch audit rule");
    const nonConformitiesResponse = nonConformitiesQuery.data ?? {nonConformities: [], total: 0};

    const refetchNonConformities = nonConformitiesQuery.refetch;

    const scanMutation = useScanAuditRule();

    const [scanning, setScanning] = useState(false);
    const [customToolbar, setCustomToolbar] = useState<React.ReactNode>(null);

    const rule = ruleResponse?.data?.rule;
    const allNonConformities = nonConformitiesResponse?.nonConformities ?? [];

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

    const handleFilterChange = useCallback((newSearch: string, newFilter: string) => {
        setCollectionFilter(AUDIT_STATE_KEY, { search: newSearch, expression: newFilter });
    }, [setCollectionFilter]);

    const hasCustomPage = !!rule?.customPage;
    const CustomComponent = hasCustomPage ? AUDIT_CUSTOM_COMPONENTS[rule!.customPage!] : null;

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <Group justify="space-between" mb="md">
                <Group>
                    <Title order={2}>{rule?.name ?? 'Audit Rule'}</Title>
                    <Badge color={rule?.nonConformityCount ? 'red' : 'green'}>
                        {rule?.nonConformityCount ?? 0} issues
                    </Badge>
                </Group>
                <Group>
                    {customToolbar}
                    <Button
                        leftSection={<IconRefresh size={16}/>}
                        onClick={handleScan}
                        loading={scanning}
                    >
                        Run Scan
                    </Button>
                </Group>
            </Group>

            <Text c="dimmed" mb="md">{rule?.description}</Text>

            <div style={{flex: 1, minHeight: 0}}>
                {CustomComponent ? (
                    <CustomComponent onToolbarChange={setCustomToolbar} />
                ) : (
                    <AuditDefaultComponent
                        nonConformities={allNonConformities}
                        ruleId={id}
                        refetchNonConformities={refetchNonConformities}
                        serverSearch={appliedSearch}
                        serverFilter={appliedFilter}
                        onServerFilterChange={handleFilterChange}
                    />
                )}
            </div>
        </div>
    );
}
