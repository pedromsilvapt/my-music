import {Badge, Button, Group, Modal, ScrollArea, Stack, Text, ThemeIcon} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {IconHeart, IconTrash} from "@tabler/icons-react";
import {useCallback} from "react";
import {useTranslation} from "react-i18next";
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
    const {t} = useTranslation(["wishlist", "common"]);
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
                    const errorDetail = responseData?.detail || t("wishlist:modal.unknownError");
                    notifications.show({
                        title: t("common:status.error"),
                        message: t("wishlist:modal.addFailed", {error: errorDetail}),
                        color: "red"
                    });
                    return;
                }
                
                notifications.show({
                    title: t("wishlist:modal.addedTitle"),
                    message: t("wishlist:modal.addedMessage", {query: currentQuery}),
                    color: "green"
                });
                onClose();
            },
            onError: (error) => {
                notifications.show({
                    title: t("common:status.error"),
                    message: t("wishlist:modal.addFailed", {error: String(error)}),
                    color: "red"
                });
            }
        });
    }, [currentSource, currentQuery, currentFilter, createMutation, onClose, t]);

    const handleKeep = useCallback((id: number) => {
        updateMutation.mutate({id}, {
            onSuccess: () => {
                notifications.show({
                    title: t("wishlist:modal.updatedTitle"),
                    message: t("wishlist:modal.updatedMessage"),
                    color: "green"
                });
            },
            onError: (error) => {
                notifications.show({
                    title: t("common:status.error"),
                    message: t("wishlist:modal.updateFailed", {error: String(error)}),
                    color: "red"
                });
            }
        });
    }, [updateMutation, t]);

    const handleDelete = useCallback((id: number) => {
        deleteMutation.mutate({id}, {
            onSuccess: () => {
                notifications.show({
                    title: t("wishlist:modal.removedTitle"),
                    message: t("wishlist:modal.deletedMessage"),
                    color: "green"
                });
            },
            onError: (error) => {
                notifications.show({
                    title: t("common:status.error"),
                    message: t("wishlist:modal.deleteFailed", {error: String(error)}),
                    color: "red"
                });
            }
        });
    }, [deleteMutation, t]);

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
                    <Text fw={500}>{t("wishlist:modal.title")}</Text>
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
                        {t("wishlist:modal.addCurrentSearch")}
                    </Button>
                )}

                {!hasItems && !isPending && (
                    <Text c="dimmed" ta="center" size="sm">
                        {t("wishlist:modal.empty")}
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
                                                        {t("wishlist:modal.filterLabel", {filter: item.filter})}
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
                                                    {t("wishlist:modal.badgeUpdated")}
                                                </Badge>
                                            )}
                                            {item.status === WishlistItemStatus.Active && (
                                                <Badge color="green" variant="light">
                                                    {t("wishlist:modal.badgeActive")}
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
                                                    {t("common:common.keep")}
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