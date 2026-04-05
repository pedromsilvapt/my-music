import {Badge, Button, Group, Modal, ScrollArea, Stack, Text, ThemeIcon} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {IconHeart, IconTrash} from "@tabler/icons-react";
import {useCallback} from "react";
import {useListSources} from "../../client/sources";
import {useCreateWishlistMutation, useRemoveWishlistMutation, useUpdateWishlistMutation, useWishlist} from "../../hooks/use-wishlist";
import type {ListSourceItem} from "../../model";
import {WishlistItemStatus} from "../../model";
import {ZINDEX_MODAL} from "../../consts.ts";
import TablerIcon from "../common/tabler-icon.tsx";

interface WishlistModalProps {
    opened: boolean;
    onClose: () => void;
    currentSource?: ListSourceItem | null;
    currentQuery: string;
    currentFilter: string;
    onItemClick?: (sourceId: number, query: string) => void;
}

export default function WishlistModal({
    opened,
    onClose,
    currentSource,
    currentQuery,
    currentFilter,
    onItemClick
}: WishlistModalProps) {
    const {data: wishlistResponse, isPending} = useWishlist();
    const {data: sourcesResponse} = useListSources();
    const createMutation = useCreateWishlistMutation();
    const updateMutation = useUpdateWishlistMutation();
    const deleteMutation = useRemoveWishlistMutation();

    const items = wishlistResponse?.data?.items ?? [];
    const sources = sourcesResponse?.data?.sources ?? [];
    const sourcesMap = new Map(sources.map(s => [s.id, s]));

    const handleAddCurrentSearch = useCallback(() => {
        if (!currentSource || !currentQuery.trim()) {
            return;
        }

        // Only store filter if it's not empty
        const filterToStore = currentFilter.trim() || undefined;

        createMutation.mutate({
            data: {
                sourceId: currentSource.id,
                query: currentQuery.trim(),
                filter: filterToStore
            }
        }, {
            onSuccess: (response) => {
                if (response.status >= 400) {
                    const responseData = response.data as { detail?: string } | undefined;
                    const errorDetail = responseData?.detail || "Unknown error";
                    notifications.show({
                        title: "Error",
                        message: `Failed to add to wishlist: ${errorDetail}`,
                        color: "red"
                    });
                    return;
                }
                
                notifications.show({
                    title: "Added to Wishlist",
                    message: `Tracking "${currentQuery}" for changes`,
                    color: "green"
                });
                onClose();
            },
            onError: (error) => {
                notifications.show({
                    title: "Error",
                    message: `Failed to add to wishlist: ${error}`,
                    color: "red"
                });
            }
        });
    }, [currentSource, currentQuery, currentFilter, createMutation, onClose]);

    const handleKeep = useCallback((id: number) => {
        updateMutation.mutate({id}, {
            onSuccess: () => {
                notifications.show({
                    title: "Updated",
                    message: "Wishlist item hash updated",
                    color: "green"
                });
            },
            onError: (error) => {
                notifications.show({
                    title: "Error",
                    message: `Failed to update: ${error}`,
                    color: "red"
                });
            }
        });
    }, [updateMutation]);

    const handleDelete = useCallback((id: number) => {
        deleteMutation.mutate({id}, {
            onSuccess: () => {
                notifications.show({
                    title: "Removed",
                    message: "Wishlist item deleted",
                    color: "green"
                });
            },
            onError: (error) => {
                notifications.show({
                    title: "Error",
                    message: `Failed to delete: ${error}`,
                    color: "red"
                });
            }
        });
    }, [deleteMutation]);

    const canAddCurrentSearch = currentSource && currentQuery.trim();
    const hasItems = items.length > 0;

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title={
                <Group gap="xs">
                    <ThemeIcon variant="light" color="red">
                        <IconHeart size={16}/>
                    </ThemeIcon>
                    <Text fw={500}>Wishlist</Text>
                </Group>
            }
            size="lg"
            centered
            zIndex={ZINDEX_MODAL}
        >
            <Stack gap="md">
                {canAddCurrentSearch && (
                    <Button
                        variant="light"
                        fullWidth
                        onClick={handleAddCurrentSearch}
                        loading={createMutation.isPending}
                    >
                        Add Current Search
                    </Button>
                )}

                {!hasItems && !isPending && (
                    <Text c="dimmed" ta="center" size="sm">
                        No wishlist items yet. Search for songs and click "Add Current Search" to track results.
                    </Text>
                )}

                {hasItems && (
                    <ScrollArea.Autosize mah={400}>
                        <Stack gap="xs">
                            {items.map((item) => {
                                const source = sourcesMap.get(item.sourceId);
                                return (
                                    <Group key={item.id} justify="space-between" p="sm" style={{
                                        borderRadius: 4,
                                        border: '1px solid var(--mantine-color-default-border)',
                                        cursor: 'pointer'
                                    }} onClick={() => {
                                        onItemClick?.(item.sourceId, item.query);
                                        onClose();
                                    }}>
                                        <Group gap="sm" style={{flex: 1, minWidth: 0}}>
                                            {source && (
                                                <ThemeIcon variant="light" size="sm">
                                                    <TablerIcon icon={source.icon} size={16}/>
                                                </ThemeIcon>
                                            )}
                                            <Stack gap={2} style={{flex: 1, minWidth: 0}}>
                                                <Text size="sm" lineClamp={1}>
                                                    {item.query}
                                                </Text>
                                                {item.filter && (
                                                    <Text size="xs" c="blue" lineClamp={1}>
                                                        Filter: {item.filter}
                                                    </Text>
                                                )}
                                                {source && (
                                                    <Text size="xs" c="dimmed">
                                                        {source.name}
                                                    </Text>
                                                )}
                                            </Stack>
                                            {item.status === WishlistItemStatus.Updated && (
                                                <Badge color="yellow" variant="light">
                                                    Updated
                                                </Badge>
                                            )}
                                            {item.status === WishlistItemStatus.Active && (
                                                <Badge color="green" variant="light">
                                                    Active
                                                </Badge>
                                            )}
                                        </Group>
                                        <Group gap="xs">
                                            {item.status === WishlistItemStatus.Updated && (
                                                <Button
                                                    size="xs"
                                                    variant="light"
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        handleKeep(item.id);
                                                    }}
                                                    loading={updateMutation.isPending}
                                                >
                                                    Keep
                                                </Button>
                                            )}
                                            <Button
                                                size="xs"
                                                variant="subtle"
                                                color="red"
                                                onClick={(e) => {
                                                    e.stopPropagation();
                                                    handleDelete(item.id);
                                                }}
                                                loading={deleteMutation.isPending}
                                            >
                                                <IconTrash size={14}/>
                                            </Button>
                                        </Group>
                                    </Group>
                                );
                            })}
                        </Stack>
                    </ScrollArea.Autosize>
                )}
            </Stack>
        </Modal>
    );
}