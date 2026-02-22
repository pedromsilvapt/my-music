import {Text, Tooltip} from "@mantine/core";
import {IconBasketDown} from "@tabler/icons-react";
import {useMemo} from "react";
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
                displayName: 'Title',
                render: row => <SongTitle title={row.title} isExplicit={row.explicit}/>,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'artists',
                displayName: 'Artists',
                render: row => <SongArtists artists={row.artists}/>,
                width: '1fr',
                sortable: true,
                getValue: song => song.artists?.[0]?.name,
            },
            {
                name: 'album',
                displayName: 'Album',
                render: row => <SongAlbum name={row.album?.name ?? '(no album)'}/>,
                width: '1fr',
                sortable: true,
                getValue: song => song.album?.name,
            },
            {
                name: 'year',
                displayName: 'Year',
                render: row => row.year,
                sortable: true,
                getValue: song => song.year ?? 0,
            },
            {
                name: 'duration',
                displayName: 'Duration',
                render: row => row.duration,
                sortable: true,
                getValue: song => song.duration ?? '',
            },
            {
                name: 'price',
                displayName: 'Price',
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
                    renderLabel: () => "Purchase",
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
        renderListSubTitle: (row) => <SongSubTitle artists={row.artists} album={row.album} year={row.year} c="gray"/>,
    }) as CollectionSchema<SourceSong>, [onPurchase, filterMetadata]);
}
