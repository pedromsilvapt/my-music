import {ActionIcon, Group, Text} from "@mantine/core";
import {IconChevronLeft, IconChevronRight} from "@tabler/icons-react";

interface EditorPagerProps {
    /** Index of the item being edited. */
    index: number;
    count: number;
    /** Name of the item being edited, as it was before any change. */
    label: string;
    /** Whether the item being edited has unsaved changes. */
    modified: boolean;
    onChange: (index: number) => void;
    previousLabel: string;
    nextLabel: string;
    testId: string;
}

/**
 * Steps through the items of an editor opened for several of them, one item at a time.
 */
export default function EditorPager({
    index,
    count,
    label,
    modified,
    onChange,
    previousLabel,
    nextLabel,
    testId,
}: EditorPagerProps) {
    return (
        <Group gap="xs" data-testid={testId} data-index={index} data-count={count}>
            <ActionIcon
                variant="light"
                onClick={() => onChange(index - 1)}
                disabled={index === 0}
                aria-label={previousLabel}
                data-testid={`${testId}-previous`}
            >
                <IconChevronLeft/>
            </ActionIcon>
            <Text size="sm" c="dimmed">
                {label}
                {modified && " *"}
            </Text>
            <ActionIcon
                variant="light"
                onClick={() => onChange(index + 1)}
                disabled={index === count - 1}
                aria-label={nextLabel}
                data-testid={`${testId}-next`}
            >
                <IconChevronRight/>
            </ActionIcon>
        </Group>
    );
}
