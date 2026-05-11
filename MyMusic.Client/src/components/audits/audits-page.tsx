import {Badge, Button, Group, Paper, Text, Title} from "@mantine/core";
import {IconClipboardCheck, IconRefresh} from '@tabler/icons-react';
import {Link} from "@tanstack/react-router";
import {useState} from "react";
import {useListAuditRules} from "../../client/audits.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {useBatchMetadataFetch} from "../../hooks/useBatchMetadataFetch";
import {TaskMonitorLink} from "./task-monitor-link";

export default function AuditsPage() {
    const auditRulesQuery = useListAuditRules();
    const batchFetch = useBatchMetadataFetch();
    const [message, setMessage] = useState<string | null>(null);

    const auditRulesResponse = useQueryData(
        auditRulesQuery,
        "Failed to fetch audit rules"
    ) ?? {data: {rules: []}};

    const rules = auditRulesResponse?.data?.rules ?? [];

    const handleAutoFetch = () => {
        batchFetch.mutate(undefined, {
            onSuccess: (response) => {
                setMessage(response.message);
                setTimeout(() => setMessage(null), 5000);
            }
        });
    };

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}} data-testid="audits">
            <Group justify="space-between" mb="md">
                <Title order={2}>Audits</Title>
                <Group gap="sm">
                    <TaskMonitorLink />
                    <Button
                        leftSection={<IconRefresh size={18}/>}
                        onClick={handleAutoFetch}
                        loading={batchFetch.isPending}
                        variant="light"
                    >
                        Auto-fetch Metadata
                    </Button>
                </Group>
            </Group>

            {message && (
                <Paper p="sm" mb="md" withBorder bg="green.0">
                    <Text c="green.7">{message}</Text>
                </Paper>
            )}

            <div style={{flex: 1, overflow: 'auto'}}>
                {rules.map((rule) => (
                    <Paper
                        key={rule.id}
                        component={Link}
                        to={`/audits/${rule.id}`}
                        shadow="xs"
                        p="md"
                        mb="sm"
                        withBorder
                        style={{cursor: 'pointer', textDecoration: 'none', color: 'inherit'}}
                    >
                        <Group justify="space-between" wrap="nowrap">
                            <Group>
                                <IconClipboardCheck size={24}/>
                                <div>
                                    <Text fw={500}>{rule.name}</Text>
                                    <Text size="sm" c="dimmed">{rule.description}</Text>
                                </div>
                            </Group>
                            <Badge color={rule.nonConformityCount > 0 ? 'red' : 'green'}>
                                {rule.nonConformityCount} issues
                            </Badge>
                        </Group>
                    </Paper>
                ))}

                {rules.length === 0 && (
                    <Text c="dimmed" ta="center" mt="xl">
                        No audit rules available
                    </Text>
                )}
            </div>
        </div>
    );
}
