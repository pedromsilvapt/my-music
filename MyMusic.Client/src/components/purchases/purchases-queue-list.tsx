import {Button, Group, Stack,} from '@mantine/core';
import {IconShoppingCartX} from "@tabler/icons-react";
import {type ListPurchasesItem} from "../../model";
import CollectionToolbar from "../common/collection/collection-toolbar.tsx";
import Collection from "../common/collection/collection.tsx";
import usePurchasedSongsQuery from "./usePurchasedSongsQuery.tsx";
import {usePurchasedSongsSchema} from "./usePurchasedSongsSchema.tsx";

interface PurchasesQueueListProps {
    onRequeue: (purchases: ListPurchasesItem[]) => void;
    onDownload: (purchases: ListPurchasesItem[]) => void;
    onClear: (purchases: ListPurchasesItem[]) => void;
    onClearCompleted: () => void;
    onClearAll: () => void;
    refreshInterval?: number; // in milliseconds, default 5000
}

export default function PurchasesQueueList({
                                               onRequeue,
                                               onDownload,
                                               onClear,
                                               onClearCompleted,
                                               onClearAll,
                                           }: PurchasesQueueListProps) {
    const {data: data} = usePurchasedSongsQuery();

    const purchasesSchema = usePurchasedSongsSchema(
        onRequeue,
        onDownload,
        onClear,
    );

    const purchases = data?.data?.purchases ?? [];

    const hasCompletedOrFailed = purchases.some(
        (song) => song.status === 'Completed' || song.status === 'Failed'
    );

    const hasAnySongs = purchases.some(
        (song) => song.status !== 'Acquiring'
    );

    return <Collection
        key="artists"
        items={purchases}
        schema={purchasesSchema}
        initialView="list"
        toolbar={(p) =>
            <CollectionToolbar {...p}
                               renderLeftSection={() => null}
                               renderMiddleSection={() => <Group justify="flex-start">
                                   <Button
                                       variant="light"
                                       color="gray"
                                       onClick={onClearCompleted}
                                       disabled={!hasCompletedOrFailed}
                                       leftSection={<IconShoppingCartX/>}
                                   >
                                       Clear Completed
                                   </Button>
                                   <Button
                                       variant="light"
                                       color="red"
                                       onClick={onClearAll}
                                       disabled={!hasAnySongs}
                                       leftSection={<IconShoppingCartX/>}
                                   >
                                       Clear All
                                   </Button>
                               </Group>}
            />
        }
    >
    </Collection>;

    return (
        <Stack gap="md">
            {/* Action Buttons */}
            <Group justify="flex-end">
                <Button
                    variant="light"
                    color="gray"
                    onClick={onClearCompleted}
                    disabled={!hasCompletedOrFailed}
                >
                    Clear Completed
                </Button>
                <Button
                    variant="light"
                    color="red"
                    onClick={onClearAll}
                    disabled={!hasAnySongs}
                >
                    Clear All
                </Button>
            </Group>

            {/* Songs List */}

            {/*<Stack gap="sm">*/}
            {/*    {purchases.length === 0 ? (*/}
            {/*        <Paper p="xl" withBorder>*/}
            {/*            <Text c="dimmed" ta="center">*/}
            {/*                No purchased songs*/}
            {/*            </Text>*/}
            {/*        </Paper>*/}
            {/*    ) : (*/}
            {/*        purchases.map((song) => (*/}
            {/*            <Paper*/}
            {/*                key={song.id}*/}
            {/*                p="md"*/}
            {/*                withBorder*/}
            {/*                shadow="xs"*/}
            {/*                style={{*/}
            {/*                    opacity: isRefreshing ? 0.7 : 1,*/}
            {/*                    transition: 'opacity 0.2s ease',*/}
            {/*                }}*/}
            {/*            >*/}
            {/*                <Group justify="space-between" align="flex-start" wrap="nowrap">*/}
            {/*                    /!* Left side: Icon and song info *!/*/}
            {/*                    <Group gap="md" align="flex-start" style={{flex: 1}}>*/}
            {/*                        /!* Source Icon *!/*/}
            {/*                        <Box*/}
            {/*                            style={{*/}
            {/*                                width: 40,*/}
            {/*                                height: 40,*/}
            {/*                                display: 'flex',*/}
            {/*                                alignItems: 'center',*/}
            {/*                                justifyContent: 'center',*/}
            {/*                            }}*/}
            {/*                        >*/}
            {/*                            {song.sourceIcon}*/}
            {/*                        </Box>*/}

            {/*                        /!* Song Details *!/*/}
            {/*                        <Stack gap={4} style={{flex: 1}}>*/}
            {/*                            <Text fw={600} size="md">*/}
            {/*                                {song.title}*/}
            {/*                            </Text>*/}
            {/*                            <Text size="sm" c="dimmed">*/}
            {/*                                {song.subTitle}*/}
            {/*                            </Text>*/}
            {/*                            <Text size="xs" c="dimmed">*/}
            {/*                                {song.sourceName}*/}
            {/*                            </Text>*/}

            {/*                            /!* Error message if failed *!/*/}
            {/*                            {song.status === 'Failed' && song.errorMessage && (*/}
            {/*                                <Alert*/}
            {/*                                    icon={<IconAlertCircle size={16}/>}*/}
            {/*                                    color="red"*/}
            {/*                                    variant="light"*/}
            {/*                                    mt="xs"*/}
            {/*                                >*/}
            {/*                                    {song.errorMessage}*/}
            {/*                                </Alert>*/}
            {/*                            )}*/}
            {/*                        </Stack>*/}
            {/*                    </Group>*/}

            {/*                    /!* Right side: Status and actions *!/*/}
            {/*                    <Stack gap="xs" align="flex-end">*/}
            {/*                        {getStatusBadge(song.status)}*/}

            {/*                        <Group gap="xs">*/}
            {/*                            /!* Requeue button for failed songs *!/*/}
            {/*                            {song.status === 'Failed' && (*/}
            {/*                                <ActionIcon*/}
            {/*                                    variant="light"*/}
            {/*                                    color="blue"*/}
            {/*                                    size="lg"*/}
            {/*                                    onClick={() => onRequeue(song.id)}*/}
            {/*                                    title="Requeue"*/}
            {/*                                >*/}
            {/*                                    <IconRefresh size={18}/>*/}
            {/*                                </ActionIcon>*/}
            {/*                            )}*/}

            {/*                            /!* Download button for completed songs *!/*/}
            {/*                            {song.status === 'Completed' && (*/}
            {/*                                <ActionIcon*/}
            {/*                                    variant="light"*/}
            {/*                                    color="green"*/}
            {/*                                    size="lg"*/}
            {/*                                    onClick={() => onDownload(song.id)}*/}
            {/*                                    title="Download"*/}
            {/*                                >*/}
            {/*                                    <IconDownload size={18}/>*/}
            {/*                                </ActionIcon>*/}
            {/*                            )}*/}
            {/*                        </Group>*/}
            {/*                    </Stack>*/}
            {/*                </Group>*/}
            {/*            </Paper>*/}
            {/*        ))*/}
            {/*    )}*/}
            {/*</Stack>*/}
        </Stack>
    );
};
