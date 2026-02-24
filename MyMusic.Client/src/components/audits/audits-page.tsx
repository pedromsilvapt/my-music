import {Badge, Group, Paper, Text, Title} from "@mantine/core";
import {IconClipboardCheck} from '@tabler/icons-react';
import {Link} from "@tanstack/react-router";
import {useListAuditRules} from "../../client/audits.ts";

export default function AuditsPage() {
    const {data} = useListAuditRules();

    const rules = data?.data?.rules ?? [];

    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <Group justify="space-between" mb="md">
                <Title order={2}>Audits</Title>
            </Group>

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
