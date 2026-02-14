import {Badge, Text, Tooltip} from "@mantine/core";
import {
    IconCheck,
    IconClock,
    IconDownload,
    IconLoader,
    IconMusic,
    IconRefresh,
    IconShoppingBagX,
    IconX
} from "@tabler/icons-react";
import {useMemo} from "react";
import {type ListPurchasesItem, PurchasedSongStatus} from "../../model";
import Artwork from "../common/artwork.tsx";
import type {CollectionSchemaAction} from "../common/collection/collection-schema.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import SongArtwork from "../common/fields/song-artwork.tsx";

export function usePurchasedSongsSchema(
    onRequeue: (purchases: ListPurchasesItem[]) => void,
    onDownload: (purchases: ListPurchasesItem[]) => void,
    onClear: (purchases: ListPurchasesItem[]) => void,
) {
    return useMemo(() => ({
        key: row => row.id,
        searchVector: purchase => purchase.title + " " + purchase.subTitle,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: row => <SongArtwork url={row.cover} size={32}/>,
                width: '52px',
            },
            {
                name: 'title',
                displayName: 'Title',
                render: row =>
                    <Tooltip label={row.title} openDelay={500}><Text>{row.title}</Text></Tooltip>,
                width: '1fr',
            },
            {
                name: 'subTitle',
                displayName: 'SubTitle',
                render: row => row.subTitle,
                width: '2fr',
                align: 'center',
            },
            {
                name: 'status',
                displayName: 'Status',
                render: row => getStatusBadge(row.status),
                width: '200px',
                align: 'center',
            }
        ],

        actions: (purchases) => {
            const buttons: CollectionSchemaAction<ListPurchasesItem>[] = [];

            if (purchases.some(p => p.status == PurchasedSongStatus.Completed && p.songId != null)) {
                buttons.push({
                    name: "download",
                    renderIcon: () => <IconDownload/>,
                    renderLabel: () => "Download",
                    onClick: () => onDownload(purchases),
                    primary: true,
                });
            }

            if (purchases.some(p => p.status == PurchasedSongStatus.Failed)) {
                buttons.push({
                    name: "requeue",
                    renderIcon: () => <IconRefresh/>,
                    renderLabel: () => "Requeue",
                    onClick: () => onRequeue(purchases),
                    primary: true,
                });
            }

            if (purchases.some(p => p.status != PurchasedSongStatus.Acquiring)) {
                buttons.push({
                    name: "clear",
                    renderIcon: () => <IconShoppingBagX/>,
                    renderLabel: () => "Clear",
                    onClick: () => onClear(purchases),
                    primary: true,
                })
            }

            return buttons;
        },

        estimateListRowHeight: () => 100,
        renderListArtwork: (row, size) => <Artwork
            url={row.cover}
            size={size}
            placeholderIcon={<IconMusic/>}
        />,
        renderListTitle: (row) => <Tooltip label={row.title} openDelay={500}>
            <Text>{row.title}</Text>
        </Tooltip>,
        renderListSubTitle: (row) => <>
            <Text c="gray">{row.subTitle}</Text>
            {getStatusBadge(row.status)}
        </>,
    }) as CollectionSchema<ListPurchasesItem>, [onRequeue]);
}


// Helper function to get status badge
const getStatusBadge = (status: PurchasedSongStatus) => {
    const config = {
        [PurchasedSongStatus.Queued]: {color: 'blue', icon: IconClock, label: 'Queued'},
        [PurchasedSongStatus.Acquiring]: {color: 'yellow', icon: IconLoader, label: 'Acquiring'},
        [PurchasedSongStatus.Completed]: {color: 'green', icon: IconCheck, label: 'Completed'},
        [PurchasedSongStatus.Failed]: {color: 'red', icon: IconX, label: 'Failed'},
    };

    const {color, icon: Icon, label} = config[status];

    return (
        <Badge
            color={color}
            variant="light"
            leftSection={<Icon size={14}/>}
            size="md"
        >
            {label}
        </Badge>
    );
};
