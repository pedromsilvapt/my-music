import {Text, Tooltip} from "@mantine/core";
import {IconBasketDown} from "@tabler/icons-react";
import {useMemo} from "react";
import {useTranslation} from "react-i18next";
import {type SourceSong} from "../../model";
import {type CollectionSchema} from "../common/collection/collection.tsx";
import SongAlbum from "../common/fields/song-album.tsx";
import SongArtists from "../common/fields/song-artists.tsx";
import SongArtwork from "../common/fields/song-artwork.tsx";
import SongSubTitle from "../common/fields/song-sub-title.tsx";
import SongTitle from "../common/fields/song-title.tsx";
import {useFilterMetadata} from "../filters/use-filter-metadata.ts";

export function useSourceSongsSchema(
    onPurchase: (songs: SourceSong[]) => void,
) {
    const {t} = useTranslation(["sources", "common"]);
    const {data: filterMetadata} = useFilterMetadata('sources');

    return useMemo(() => ({
        key: row => row.id,
        searchVector: purchase => purchase.title,
        filterMetadata,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: row => <SongArtwork url={row.cover?.smallest}/>,
                width: 52,
            },
            {
                name: 'title',
                displayName: t("sources:schema.columns.title"),
                render: row => <SongTitle title={row.title} link={row.link} isExplicit={row.explicit}/>,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'artists',
                displayName: t("sources:schema.columns.artists"),
                render: row => <SongArtists artists={row.artists}/>,
                width: '1fr',
                sortable: true,
                getValue: song => song.artists?.[0]?.name,
            },
            {
                name: 'album',
                displayName: t("sources:schema.columns.album"),
                render: row => <SongAlbum name={row.album?.name ?? t("sources:schema.noAlbum")} link={row.album?.link}/>,
                width: '1fr',
                sortable: true,
                getValue: song => song.album?.name,
            },
            {
                name: 'year',
                displayName: t("sources:schema.columns.year"),
                render: row => row.year,
                sortable: true,
                getValue: song => song.year ?? 0,
            },
            {
                name: 'duration',
                displayName: t("sources:schema.columns.duration"),
                render: row => row.duration,
                sortable: true,
                getValue: song => song.duration ?? '',
            },
            {
                name: 'price',
                displayName: t("sources:schema.columns.price"),
                render: row => row.price?.toFixed(2) ?? '-',
                sortable: true,
                getValue: song => song.price ?? 0,
            },
        ],

        actions: () => {
            return [
                {
                    name: "purchase",
                    renderIcon: () => <IconBasketDown/>,
                    renderLabel: () => t("sources:schema.purchase"),
                    onClick: (songs) => onPurchase(songs),
                    primary: true
                }
            ];
        },

        estimateListRowHeight: () => 84,
        renderListArtwork: (row, size) => <SongArtwork url={row.cover?.normal} size={size}/>,
        renderListTitle: (row) => <Tooltip label={row.title} openDelay={500}>
            <Text>{row.title}</Text>
        </Tooltip>,
        renderListSubTitle: (row) => <SongSubTitle artists={row.artists} album={row.album ? {
            name: row.album.name,
            albumId: row.album.id,
            link: row.album.link
        } : undefined} year={row.year} c="gray"/>,
    }) as CollectionSchema<SourceSong>, [onPurchase, filterMetadata, t]);
}
