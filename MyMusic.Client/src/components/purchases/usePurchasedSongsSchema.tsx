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
import {useTranslation} from "react-i18next";
import type {TFunction} from "i18next";
import {type ListPurchaseItem, PurchasedSongStatus} from "../../model";
import Artwork from "../common/artwork.tsx";
import type {CollectionSchemaAction} from "../common/collection/collection-schema.tsx";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import SongArtwork from "../common/fields/song-artwork.tsx";

export function usePurchasedSongsSchema(
    onRequeue: (purchases: ListPurchaseItem[]) => void,
    onDownload: (purchases: ListPurchaseItem[]) => void,
    onClear: (purchases: ListPurchaseItem[]) => void,
) {
    const {t} = useTranslation(["purchases", "common"]);
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
                displayName: t("purchases:schema.columns.title"),
                render: row =>
                    <Tooltip label={row.title} openDelay={500}><Text>{row.title}</Text></Tooltip>,
                width: '1fr',
            },
            {
                name: 'subTitle',
                displayName: t("purchases:schema.columns.subTitle"),
                render: row => row.subTitle,
                width: '2fr',
                align: 'center',
            },
            {
                name: 'status',
                displayName: t("purchases:schema.columns.status"),
                render: row => getStatusBadge(row.status, t),
                width: '200px',
                align: 'center',
            }
        ],

        actions: (purchases) => {
            const buttons: CollectionSchemaAction<ListPurchaseItem>[] = [];

            if (purchases.some(p => p.status == PurchasedSongStatus.Completed && p.songId != null)) {
                buttons.push({
                    name: "download",
                    renderIcon: () => <IconDownload/>,
                    renderLabel: () => t("purchases:schema.download"),
                    onClick: () => onDownload(purchases),
                    primary: true,
                });
            }

            if (purchases.some(p => p.status == PurchasedSongStatus.Failed)) {
                buttons.push({
                    name: "requeue",
                    renderIcon: () => <IconRefresh/>,
                    renderLabel: () => t("purchases:schema.requeue"),
                    onClick: () => onRequeue(purchases),
                    primary: true,
                });
            }

            if (purchases.some(p => p.status != PurchasedSongStatus.Acquiring)) {
                buttons.push({
                    name: "clear",
                    renderIcon: () => <IconShoppingBagX/>,
                    renderLabel: () => t("purchases:schema.clear"),
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
            {getStatusBadge(row.status, t)}
        </>,
    }) as CollectionSchema<ListPurchaseItem>, [onRequeue, onDownload, onClear, t]);
}


// Helper function to get status badge
const getStatusBadge = (status: PurchasedSongStatus, t: TFunction<["purchases", "common"]>) => {
    const config = {
        [PurchasedSongStatus.Queued]: {color: 'blue', icon: IconClock, label: t("purchases:schema.status.queued")},
        [PurchasedSongStatus.Acquiring]: {color: 'yellow', icon: IconLoader, label: t("purchases:schema.status.acquiring")},
        [PurchasedSongStatus.Completed]: {color: 'green', icon: IconCheck, label: t("purchases:schema.status.completed")},
        [PurchasedSongStatus.Failed]: {color: 'red', icon: IconX, label: t("purchases:schema.status.failed")},
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
