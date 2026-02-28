import {Anchor, Text} from "@mantine/core";
import {IconDisc} from "@tabler/icons-react";
import {useNavigate} from "@tanstack/react-router";
import {useMemo} from "react";
import type {ListPlayHistoryItem} from "../../model";
import Artwork from "../common/artwork";
import type {CollectionSchema} from "../common/collection/collection";

export function usePlayHistorySchema(): CollectionSchema<ListPlayHistoryItem> {
    const navigate = useNavigate();

    return useMemo(() => ({
        key: row => row.id,
        searchVector: row => `${row.songTitle} - ${row.albumName} - ${row.artistName ?? ''} - ${row.deviceName ?? ''}`,

        estimateTableRowHeight: () => 47,
        columns: [
            {
                name: 'artwork',
                displayName: '',
                render: row => (
                    <Artwork
                        id={row.coverId ?? row.albumId}
                        size={40}
                        placeholderIcon={<IconDisc size={20}/>}
                    />
                ),
                width: 52,
            },
            {
                name: 'songTitle',
                displayName: 'Title',
                render: row => (
                    <Anchor
                        c="inherit"
                        onClick={() => navigate({to: '/songs/$songId', params: {songId: row.songId.toString()}})}
                    >
                        {row.songTitle}
                    </Anchor>
                ),
                getValue: row => row.songTitle,
                width: '2fr',
                sortable: true,
            },
            {
                name: 'artistName',
                displayName: 'Artist',
                render: row => row.artistId != null ? (
                    <Anchor
                        c="inherit"
                        onClick={() => navigate({
                            to: '/artists/$artistId',
                            params: {artistId: row.artistId!.toString()}
                        })}
                    >
                        {row.artistName}
                    </Anchor>
                ) : (row.artistName ?? '-'),
                getValue: row => row.artistName ?? '',
                width: '1fr',
            },
            {
                name: 'albumName',
                displayName: 'Album',
                render: row => (
                    <Anchor
                        c="inherit"
                        onClick={() => navigate({to: '/albums/$albumId', params: {albumId: row.albumId.toString()}})}
                    >
                        {row.albumName}
                    </Anchor>
                ),
                getValue: row => row.albumName,
                width: '1fr',
                sortable: true,
            },
            {
                name: 'deviceName',
                displayName: 'Device',
                render: row => row.deviceName ?? '-',
                getValue: row => row.deviceName ?? '',
                width: '1fr',
            },
            {
                name: 'playedAt',
                displayName: 'Played At',
                render: row => new Date(row.playedAt).toLocaleString(),
                getValue: row => row.playedAt,
                width: '1fr',
                sortable: true,
            },
        ],

        estimateListRowHeight: () => 84,
        renderListArtwork: (row) => (
            <Artwork
                id={row.coverId ?? row.albumId}
                size={64}
                placeholderIcon={<IconDisc/>}
            />
        ),
        renderListTitle: (row) => (
            <Anchor
                c="inherit"
                onClick={() => navigate({to: '/songs/$songId', params: {songId: row.songId.toString()}})}
            >
                <Text lineClamp={1}>{row.songTitle}</Text>
            </Anchor>
        ),
        renderListSubTitle: (row) => (
            <Text c="dimmed" lineClamp={1}>
                {row.artistId ? (
                    <Anchor
                        c="inherit"
                        onClick={(e) => {
                            e.stopPropagation();
                            navigate({to: '/artists/$artistId', params: {artistId: row.artistId!.toString()}});
                        }}
                    >
                        {row.artistName}
                    </Anchor>
                ) : row.artistName}
                {row.artistName && ' • '}
                {row.albumName}
                {row.deviceName && ` • ${row.deviceName}`}
            </Text>
        ),
    }) as CollectionSchema<ListPlayHistoryItem>, [navigate]);
}
