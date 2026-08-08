import {
    Button,
    Group,
    Modal,
    Progress,
    ScrollArea,
    Stack,
    Text,
    Badge,
    Card,
    Alert,
    Divider,
} from "@mantine/core";
import {
    IconAlertCircle,
    IconCheck,
    IconRefresh,
    IconX,
    IconClock,
    IconList,
    IconTrash,
} from "@tabler/icons-react";
import {useMetadataQueueStatus} from "../../hooks/useMetadataQueueStatus";
import {useRequeueFailedTasks} from "../../hooks/useRequeueFailedTasks";
import {useFailedTasks, type FailureReason} from "../../hooks/useFailedTasks";
import {useClearAllTasks} from "../../hooks/useClearAllTasks";
import {useState} from "react";
import {useTranslation} from "react-i18next";

interface TaskMonitorModalProps {
    opened: boolean;
    onClose: () => void;
}

function getFailureColor(reason: FailureReason): string {
    switch (reason) {
        case "ServiceUnavailable":
            return "yellow";
        case "NoMetadataFound":
            return "gray";
        case "NetworkError":
            return "orange";
        case "SystemError":
            return "red";
        case "Timeout":
            return "blue";
        default:
            return "gray";
    }
}

function getFailureReasonKey(reason: FailureReason): string {
    switch (reason) {
        case "ServiceUnavailable":
            return "serviceUnavailable";
        case "NoMetadataFound":
            return "noMetadataFound";
        case "NetworkError":
            return "networkError";
        case "SystemError":
            return "systemError";
        case "Timeout":
            return "timeout";
        default:
            return "networkError";
    }
}

export function TaskMonitorModal({opened, onClose}: TaskMonitorModalProps) {
    const {t} = useTranslation(["audits", "common"]);
    const {data: status, isLoading, error} = useMetadataQueueStatus();
    const {data: failedTasks, isLoading: isLoadingFailed} = useFailedTasks();
    const requeueMutation = useRequeueFailedTasks();
    const clearAllMutation = useClearAllTasks();
    const [confirmModalOpened, setConfirmModalOpened] = useState(false);

    const handleRequeue = () => {
        requeueMutation.mutate(undefined, {
            onSuccess: () => {
                // Success notification is handled by the mutation
            },
        });
    };

    const handleClearAll = () => {
        setConfirmModalOpened(true);
    };

    const confirmClearAll = () => {
        clearAllMutation.mutate(undefined, {
            onSuccess: () => {
                setConfirmModalOpened(false);
            },
        });
    };

    const progress = status && status.total > 0
        ? Math.round((status.completed / status.total) * 100)
        : 0;

    const isComplete = status && status.queued === 0 && status.processing === 0;
    const hasFailures = status && status.failed > 0;

    if (isLoading) {
        return (
            <Modal opened={opened} onClose={onClose} title={t("audits:taskMonitor.title")} size="lg">
                <Stack align="center" py="xl">
                    <Text>{t("audits:taskMonitor.loadingQueueStatus")}</Text>
                </Stack>
            </Modal>
        );
    }

    if (error) {
        return (
            <Modal opened={opened} onClose={onClose} title={t("audits:taskMonitor.title")} size="lg">
                <Alert icon={<IconAlertCircle size={16} />} title={t("common:status.error")} color="red">
                    {t("audits:taskMonitor.loadQueueStatusFailed")}
                </Alert>
            </Modal>
        );
    }

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title={t("audits:taskMonitor.title")}
            size="lg"
        >
            <Stack gap="md">
                {/* Progress Section */}
                <Card withBorder>
                    <Stack gap="sm">
                        <Group justify="space-between">
                            <Text fw={500}>{t("audits:taskMonitor.overallProgress")}</Text>
                            <Text size="sm" c="dimmed">
                                {t("audits:taskMonitor.completedOf", {completed: status?.completed, total: status?.total})}
                            </Text>
                        </Group>
                        <Progress
                            value={progress}
                            size="lg"
                            radius="xl"
                            color={isComplete ? "green" : "blue"}
                        />
                        <Group gap="xs">
                            <Badge color="blue" variant="light" leftSection={<IconClock size={12} />}>
                                {t("audits:taskMonitor.queuedCount", {count: status?.queued || 0})}
                            </Badge>
                            <Badge color="orange" variant="light">
                                {t("audits:taskMonitor.processingCount", {count: status?.processing || 0})}
                            </Badge>
                            <Badge color="green" variant="light" leftSection={<IconCheck size={12} />}>
                                {t("audits:taskMonitor.completedCount", {count: status?.completed || 0})}
                            </Badge>
                            {hasFailures && (
                                <Badge color="red" variant="light" leftSection={<IconX size={12} />}>
                                    {t("audits:taskMonitor.failedCount", {count: status?.failed})}
                                </Badge>
                            )}
                        </Group>
                    </Stack>
                </Card>

                {/* Failure Details Section */}
                {hasFailures && (
                    <Card withBorder>
                        <Stack gap="sm">
                            <Group justify="space-between">
                                <Text fw={500}>
                                    <IconList size={16} style={{marginRight: 8}} />
                                    {t("audits:taskMonitor.failedTasks")}
                                </Text>
                                <Badge color="red">{t("common:count.failures", {count: status?.failed})}</Badge>
                            </Group>
                            <Divider />
                            <ScrollArea h={200}>
                                <Stack gap="xs">
                                    {isLoadingFailed ? (
                                        <Text size="sm" c="dimmed" ta="center" py="md">
                                            {t("audits:taskMonitor.loadingFailedTasks")}
                                        </Text>
                                    ) : failedTasks && failedTasks.length > 0 ? (
                                        failedTasks.map((failure) => (
                                            <Card key={failure.taskId} withBorder p="xs">
                                                <Group justify="space-between">
                                                    <Stack gap={0}>
                                                        <Text size="sm" fw={500}>
                                                            {failure.songTitle} (ID: {failure.songId})
                                                        </Text>
                                                        <Text size="xs" c="dimmed">
                                                            {new Date(failure.failedAt).toLocaleString()}
                                                        </Text>
                                                    </Stack>
                                                    <Badge color={getFailureColor(failure.reason)} size="sm">
                                                        {t(`audits:taskMonitor.failureReasons.${getFailureReasonKey(failure.reason)}`)}
                                                    </Badge>
                                                </Group>
                                            </Card>
                                        ))
                                    ) : (
                                        <Text size="sm" c="dimmed" ta="center" py="md">
                                            {t("audits:taskMonitor.noFailedTasks")}
                                        </Text>
                                    )}
                                </Stack>
                            </ScrollArea>
                        </Stack>
                    </Card>
                )}

                {/* Completion Summary */}
                {isComplete && (
                    <Card withBorder color="green">
                        <Stack gap="sm">
                            <Text fw={500} size="lg">
                                <IconCheck size={20} style={{marginRight: 8, verticalAlign: "middle"}} />
                                {t("audits:taskMonitor.processingComplete")}
                            </Text>
                            <Text size="sm">
                                {t("audits:taskMonitor.allTasksProcessed", {count: status?.completed})}
                            </Text>
                            {hasFailures && (
                                <Alert color="yellow" icon={<IconAlertCircle size={16} />}>
                                    {t("audits:taskMonitor.failedTasksAlert", {count: status?.failed})}
                                </Alert>
                            )}
                        </Stack>
                    </Card>
                )}

                {/* Actions */}
                <Group justify="space-between" gap="sm">
                    <Button
                        variant="light"
                        color="red"
                        leftSection={<IconTrash size={16} />}
                        onClick={handleClearAll}
                        loading={clearAllMutation.isPending}
                    >
                        {t("audits:taskMonitor.clearAllTasks")}
                    </Button>
                    <Group gap="sm">
                        <Button variant="light" onClick={onClose}>
                            {t("common:actions.close")}
                        </Button>
                        {hasFailures && (
                            <Button
                                leftSection={<IconRefresh size={16} />}
                                onClick={handleRequeue}
                                loading={requeueMutation.isPending}
                                color="orange"
                            >
                                {t("audits:taskMonitor.retryFailedTasks")}
                            </Button>
                        )}
                    </Group>
                </Group>
            </Stack>

            {/* Confirmation Modal */}
            <Modal
                opened={confirmModalOpened}
                onClose={() => setConfirmModalOpened(false)}
                title={t("audits:taskMonitor.confirmClearAllTitle")}
                size="sm"
            >
                <Stack gap="md">
                    <Alert color="red" icon={<IconAlertCircle size={16} />}>
                        {t("audits:taskMonitor.confirmClearAllBody")}
                    </Alert>
                    <Group justify="flex-end" gap="sm">
                        <Button variant="light" onClick={() => setConfirmModalOpened(false)}>
                            {t("common:actions.cancel")}
                        </Button>
                        <Button color="red" onClick={confirmClearAll} loading={clearAllMutation.isPending}>
                            {t("audits:taskMonitor.clearAll")}
                        </Button>
                    </Group>
                </Stack>
            </Modal>
        </Modal>
    );
}
