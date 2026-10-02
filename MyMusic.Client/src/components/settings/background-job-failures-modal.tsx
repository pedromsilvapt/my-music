import {ActionIcon, Box, Center, Code, CopyButton, Group, Loader, Modal, Pagination, Paper, Stack, Text, Tooltip} from "@mantine/core";
import {IconCheck, IconCopy} from "@tabler/icons-react";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useListBackgroundJobFailures} from "../../client/background-jobs";
import type {ListBackgroundJobFailuresItem} from "../../model";
import {ZINDEX_MODAL} from "../../consts.ts";

const PAGE_SIZE = 20;

interface BackgroundJobFailuresModalProps {
    /** The job whose failures are shown; the modal is closed while null. */
    jobKey: string | null;
    onClose: () => void;
}

export default function BackgroundJobFailuresModal({jobKey, onClose}: BackgroundJobFailuresModalProps) {
    const {t} = useTranslation(["settings", "common"]);

    const jobName = jobKey
        ? t(`settings:backgroundJobs.jobs.${jobKey}.name`, {defaultValue: jobKey})
        : "";

    return (
        <Modal
            opened={jobKey !== null}
            onClose={onClose}
            title={t("settings:backgroundJobs.failuresTitle", {job: jobName})}
            size="xl"
            zIndex={ZINDEX_MODAL}
        >
            {/* Keyed so the page resets when another job's failures are opened */}
            {jobKey && <FailureList key={jobKey} jobKey={jobKey}/>}
        </Modal>
    );
}

function FailureList({jobKey}: { jobKey: string }) {
    const {t} = useTranslation(["settings", "common"]);
    const [page, setPage] = useState(1);

    const failuresQuery = useListBackgroundJobFailures(jobKey, {page, pageSize: PAGE_SIZE}, {
        query: {placeholderData: (previous) => previous},
    });

    const total = failuresQuery.data?.data?.total ?? 0;
    const failures = failuresQuery.data?.data?.failures ?? [];

    if (failuresQuery.isPending) {
        return (
            <Center py="xl" data-testid="background-job-failures" data-loading="true">
                <Loader size="sm"/>
            </Center>
        );
    }

    return (
        <Stack gap="md" data-testid="background-job-failures" data-loading={failuresQuery.isFetching ? "true" : "false"}>
            {failures.length === 0 && (
                <Text c="dimmed" ta="center" py="lg">{t("settings:backgroundJobs.noFailures")}</Text>
            )}

            {failures.map(failure => <FailureCard key={failure.id} failure={failure}/>)}

            {total > PAGE_SIZE && (
                <Group justify="center">
                    <Pagination value={page} onChange={setPage} total={Math.ceil(total / PAGE_SIZE)}/>
                </Group>
            )}
        </Stack>
    );
}

function FailureCard({failure}: { failure: ListBackgroundJobFailuresItem }) {
    const {t} = useTranslation(["settings", "common"]);
    const report = formatFailureReport(failure);

    return (
        <Paper withBorder p="sm" data-testid="background-job-failure">
            <Group justify="space-between" align="flex-start" wrap="nowrap" mb="xs">
                <Box>
                    <Text fw={600} size="sm">{failure.title}</Text>
                    {failure.occurredAt && (
                        <Text size="xs" c="dimmed">{new Date(failure.occurredAt).toLocaleString()}</Text>
                    )}
                </Box>
                <CopyButton value={report}>
                    {({copied, copy}) => (
                        <Tooltip label={copied ? t("settings:backgroundJobs.copied") : t("settings:backgroundJobs.copy")}>
                            <ActionIcon
                                variant="subtle"
                                color={copied ? "teal" : "gray"}
                                aria-label={t("settings:backgroundJobs.copy")}
                                onClick={copy}
                            >
                                {copied ? <IconCheck size={16}/> : <IconCopy size={16}/>}
                            </ActionIcon>
                        </Tooltip>
                    )}
                </CopyButton>
            </Group>
            <Text size="sm" c="red" mb="xs" style={{whiteSpace: "pre-wrap", wordBreak: "break-word"}}>
                {failure.message || t("settings:backgroundJobs.noMessage")}
            </Text>
            <Code block style={{whiteSpace: "pre-wrap", wordBreak: "break-word"}}>{report}</Code>
        </Paper>
    );
}

/**
 * Renders a failure as plain text, ready to be pasted into a bug report. Labels are technical field names and are
 * intentionally not translated.
 */
function formatFailureReport(failure: ListBackgroundJobFailuresItem): string {
    const lines = [
        `Title: ${failure.title}`,
        `OccurredAt: ${failure.occurredAt ?? "-"}`,
        ...failure.details.map(detail => `${detail.label}: ${detail.value ?? "-"}`),
        "",
        "Error:",
        failure.message || "-",
    ];

    return lines.join("\n");
}
