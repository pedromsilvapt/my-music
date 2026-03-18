import {Group, Skeleton, Stack} from '@mantine/core';

interface MetadataLoadingSkeletonProps {
    /** Number of field rows to show skeletons for (default: 8) */
    fieldCount?: number;
}

/**
 * Loading skeleton for metadata display in the edit modal.
 * Shows placeholder UI while auto-fetched metadata is being loaded.
 */
export function MetadataLoadingSkeleton({fieldCount = 8}: MetadataLoadingSkeletonProps) {
    return (
        <Stack gap="md">
            {/* Header skeleton */}
            <Group gap="xs" align="center">
                <Skeleton height={24} width={24} radius="sm" />
                <Skeleton height={20} width={200} radius="sm" />
            </Group>

            {/* Field skeletons */}
            {Array.from({length: fieldCount}, (_, i) => i + 1).map((fieldNum) => (
                <Group key={`metadata-field-${fieldNum}`} gap="xs" align="flex-end">
                    {/* Checkbox skeleton */}
                    <Skeleton height={24} width={24} radius="sm" mt={24} />
                    
                    {/* Old value skeleton */}
                    <Skeleton height={36} width={120} radius="sm" />
                    
                    {/* Arrow skeleton */}
                    <Skeleton height={20} width={24} radius="sm" />
                    
                    {/* New value skeleton */}
                    <Skeleton height={36} width="100%" radius="sm" style={{flex: 1}} />
                </Group>
            ))}

            {/* Footer skeleton */}
            <Group justify="space-between" mt="md">
                <Skeleton height={36} width={100} radius="sm" />
                <Group gap="xs">
                    <Skeleton height={36} width={80} radius="sm" />
                    <Skeleton height={36} width={100} radius="sm" />
                </Group>
            </Group>
        </Stack>
    );
}

/**
 * Compact loading skeleton for inline metadata display.
 * Used when showing a brief loading state.
 */
export function MetadataLoadingSkeletonCompact() {
    return (
        <Group gap="xs" align="center">
            <Skeleton height={16} width={16} radius="sm" />
            <Skeleton height={16} width={150} radius="sm" />
            <Skeleton height={16} width={80} radius="sm" />
        </Group>
    );
}

export default MetadataLoadingSkeleton;
