import {ActionIcon, Anchor, Box, Group, Table, Text, Tooltip} from "@mantine/core";
import {IconInfoCircle, IconRefresh} from "@tabler/icons-react";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useListBackgroundJobs} from "../../client/background-jobs";
import BackgroundJobFailuresModal from "./background-job-failures-modal";

/**
 * Lists the server's background jobs with their queued, processed and failed counters for the current user.
 * Counters a job does not keep are reported as null and shown as 0 with an explanatory tooltip.
 */
export default function BackgroundJobs() {
    const {t} = useTranslation(["settings", "common"]);
    const jobsQuery = useListBackgroundJobs();
    const [failuresJobKey, setFailuresJobKey] = useState<string | null>(null);

    const jobs = jobsQuery.data?.data?.jobs ?? [];

    return (
        <Box data-testid="background-jobs" data-loading={jobsQuery.isFetching ? "true" : "false"}>
            <Group justify="space-between" align="flex-start" mb="sm">
                <Box>
                    <Text fw={600} size="lg" mb="xs">{t("settings:backgroundJobs.title")}</Text>
                    <Text size="sm" c="dimmed">{t("settings:backgroundJobs.help")}</Text>
                </Box>
                <Tooltip label={t("settings:backgroundJobs.refresh")}>
                    <ActionIcon
                        variant="subtle"
                        aria-label={t("settings:backgroundJobs.refresh")}
                        loading={jobsQuery.isFetching}
                        onClick={() => void jobsQuery.refetch()}
                    >
                        <IconRefresh size={18}/>
                    </ActionIcon>
                </Tooltip>
            </Group>

            <Table striped highlightOnHover>
                <Table.Thead>
                    <Table.Tr>
                        <Table.Th>{t("settings:backgroundJobs.job")}</Table.Th>
                        <Table.Th ta="right">{t("settings:backgroundJobs.queued")}</Table.Th>
                        <Table.Th ta="right">{t("settings:backgroundJobs.processed")}</Table.Th>
                        <Table.Th ta="right">{t("settings:backgroundJobs.failed")}</Table.Th>
                    </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                    {jobs.map(job => (
                        <Table.Tr key={job.key} data-testid={`background-job-${job.key}`}>
                            <Table.Td>
                                <Text size="sm">
                                    {t(`settings:backgroundJobs.jobs.${job.key}.name`, {defaultValue: job.key})}
                                </Text>
                                <Text size="xs" c="dimmed">
                                    {t(`settings:backgroundJobs.jobs.${job.key}.description`, {defaultValue: ""})}
                                </Text>
                            </Table.Td>
                            <Table.Td ta="right"><Counter value={job.queued}/></Table.Td>
                            <Table.Td ta="right"><Counter value={job.processed}/></Table.Td>
                            <Table.Td ta="right">
                                {job.failed !== null && job.failed > 0 ? (
                                    <Anchor
                                        component="button"
                                        size="sm"
                                        c="red"
                                        fw={600}
                                        data-testid={`background-job-failed-${job.key}`}
                                        onClick={() => setFailuresJobKey(job.key)}
                                    >
                                        {job.failed}
                                    </Anchor>
                                ) : (
                                    <Counter value={job.failed}/>
                                )}
                            </Table.Td>
                        </Table.Tr>
                    ))}
                </Table.Tbody>
            </Table>

            <BackgroundJobFailuresModal jobKey={failuresJobKey} onClose={() => setFailuresJobKey(null)}/>
        </Box>
    );
}

function Counter({value}: { value: number | null }) {
    const {t} = useTranslation(["settings"]);

    if (value !== null) {
        return <Text size="sm">{value}</Text>;
    }

    return (
        <Group gap={4} justify="flex-end" wrap="nowrap">
            <Text size="sm" c="dimmed">0</Text>
            <Tooltip label={t("settings:backgroundJobs.notTracked")} multiline w={260}>
                <IconInfoCircle size={14} style={{opacity: 0.6}} aria-label={t("settings:backgroundJobs.notTracked")}/>
            </Tooltip>
        </Group>
    );
}
