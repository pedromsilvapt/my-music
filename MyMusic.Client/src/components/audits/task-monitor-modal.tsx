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

export function TaskMonitorModal({opened, onClose}: TaskMonitorModalProps) {
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
            <Modal opened={opened} onClose={onClose} title="Metadata Fetch Progress" size="lg">
                <Stack align="center" py="xl">
                    <Text>Loading queue status...</Text>
                </Stack>
            </Modal>
        );
    }

    if (error) {
        return (
            <Modal opened={opened} onClose={onClose} title="Metadata Fetch Progress" size="lg">
                <Alert icon={<IconAlertCircle size={16} />} title="Error" color="red">
                    Failed to load queue status. Please try again later.
                </Alert>
            </Modal>
        );
    }

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title="Metadata Fetch Progress"
            size="lg"
        >
            <Stack gap="md">
                {/* Progress Section */}
                <Card withBorder>
                    <Stack gap="sm">
                        <Group justify="space-between">
                            <Text fw={500}>Overall Progress</Text>
                            <Text size="sm" c="dimmed">
                                {status?.completed} / {status?.total} completed
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
                                {status?.queued || 0} queued
                            </Badge>
                            <Badge color="orange" variant="light">
                                {status?.processing || 0} processing
                            </Badge>
                            <Badge color="green" variant="light" leftSection={<IconCheck size={12} />}>
                                {status?.completed || 0} completed
                            </Badge>
                            {hasFailures && (
                                <Badge color="red" variant="light" leftSection={<IconX size={12} />}>
                                    {status?.failed} failed
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
                                    Failed Tasks
                                </Text>
                                <Badge color="red">{status?.failed} failures</Badge>
                            </Group>
                            <Divider />
                            <ScrollArea h={200}>
                                <Stack gap="xs">
                                    {isLoadingFailed ? (
                                        <Text size="sm" c="dimmed" ta="center" py="md">
                                            Loading failed tasks...
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
                                                        {failure.reason.replace(/([A-Z])/g, ' $1').trim()}
                                                    </Badge>
                                                </Group>
                                            </Card>
                                        ))
                                    ) : (
                                        <Text size="sm" c="dimmed" ta="center" py="md">
                                            No failed tasks to display.
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
                                Processing Complete
                            </Text>
                            <Text size="sm">
                                All tasks have been processed. {status?.completed} songs had metadata
                                fetched successfully.
                            </Text>
                            {hasFailures && (
                                <Alert color="yellow" icon={<IconAlertCircle size={16} />}>
                                    {status?.failed} task(s) failed. You can retry the failed tasks below.
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
                        Clear All Tasks & Metadata
                    </Button>
                    <Group gap="sm">
                        <Button variant="light" onClick={onClose}>
                            Close
                        </Button>
                        {hasFailures && (
                            <Button
                                leftSection={<IconRefresh size={16} />}
                                onClick={handleRequeue}
                                loading={requeueMutation.isPending}
                                color="orange"
                            >
                                Retry Failed Tasks
                            </Button>
                        )}
                    </Group>
                </Group>
            </Stack>

            {/* Confirmation Modal */}
            <Modal
                opened={confirmModalOpened}
                onClose={() => setConfirmModalOpened(false)}
                title="Confirm Clear All"
                size="sm"
            >
                <Stack gap="md">
                    <Alert color="red" icon={<IconAlertCircle size={16} />}>
                        This will permanently delete all metadata fetch tasks and all auto-fetched metadata. This action cannot be undone.
                    </Alert>
                    <Group justify="flex-end" gap="sm">
                        <Button variant="light" onClick={() => setConfirmModalOpened(false)}>
                            Cancel
                        </Button>
                        <Button color="red" onClick={confirmClearAll} loading={clearAllMutation.isPending}>
                            Clear All
                        </Button>
                    </Group>
                </Stack>
            </Modal>
        </Modal>
    );
}
