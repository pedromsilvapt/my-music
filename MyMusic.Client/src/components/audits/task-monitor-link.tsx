import {Button, Group, Badge} from "@mantine/core";
import {IconActivity, IconRefresh} from "@tabler/icons-react";
import {useState} from "react";
import {TaskMonitorModal} from "./task-monitor-modal";
import {useMetadataQueueStatus} from "../../hooks/useMetadataQueueStatus";

export function TaskMonitorLink() {
    const [modalOpen, setModalOpen] = useState(false);
    const {data: status} = useMetadataQueueStatus();

    const activeTasks = status ? status.queued + status.processing : 0;
    const hasFailures = status && status.failed > 0;

    return (
        <>
            <Button
                leftSection={<IconActivity size={18} />}
                onClick={() => setModalOpen(true)}
                variant="light"
                color={hasFailures ? "red" : activeTasks > 0 ? "blue" : "gray"}
            >
                <Group gap="xs">
                    <span>Monitor Tasks</span>
                    {activeTasks > 0 && (
                        <Badge size="sm" color="blue" circle>
                            {activeTasks}
                        </Badge>
                    )}
                    {hasFailures && (
                        <Badge size="sm" color="red" leftSection={<IconRefresh size={10} />}>
                            {status.failed} failed
                        </Badge>
                    )}
                </Group>
            </Button>
            <TaskMonitorModal opened={modalOpen} onClose={() => setModalOpen(false)} />
        </>
    );
}
