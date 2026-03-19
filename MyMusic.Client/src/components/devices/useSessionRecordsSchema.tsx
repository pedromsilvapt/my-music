import {useCallback, useMemo} from "react";
import {Badge, Code, Text, Tooltip} from "@mantine/core";
import {useQuery} from "@tanstack/react-query";
import type {SyncRecordResponseItem} from "../../model";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import type {FilterMetadataResponse} from "../filters/use-filter-metadata.ts";
import SessionRecordSong from "../common/fields/session-record-song.tsx";
import Artwork from "../common/artwork.tsx";
import {IconFileMusic} from "@tabler/icons-react";

interface RecordWithId extends SyncRecordResponseItem {
    id: string;
}

function formatDateTime(date: string | Date): string {
    const d = new Date(date);
    return d.toLocaleString();
}

function getActionColor(action: string): string {
    switch (action) {
        case 'Created':
            return 'green';
        case 'Updated':
            return 'blue';
        case 'Skipped':
            return 'gray';
        case 'Downloaded':
            return 'cyan';
        case 'Removed':
            return 'orange';
        case 'Error':
            return 'red';
        default:
            return 'gray';
    }
}

export function useSessionRecordsSchema(deviceId: number, sessionId: number) {
    // Fetch filter metadata for session records
    const {data: filterMetadata} = useQuery<FilterMetadataResponse>({
        queryKey: ["session-records-filter-metadata", deviceId, sessionId],
        queryFn: async () => {
            const response = await fetch(`/api/devices/${deviceId}/sessions/${sessionId}/records/filter-metadata`);
            if (!response.ok) {
                throw new Error("Failed to fetch filter metadata");
            }
            return response.json();
        },
        staleTime: Infinity,
    });

    // Define fetchFilterValues for auto-complete
    const fetchFilterValues = useCallback(async (field: string, searchTerm: string) => {
        const params = new URLSearchParams({field, limit: "15"});
        if (searchTerm) params.set("search", searchTerm);
        const response = await fetch(`/api/devices/${deviceId}/sessions/${sessionId}/records/filter-values?${params}`);
        if (!response.ok) return [];
        const data = await response.json();
        return data.values as string[];
    }, [deviceId, sessionId]);

    return useMemo(() => ({
        key: (row: RecordWithId) => row.id,
        searchVector: (record: RecordWithId) => record.filePath,
        filterMetadata,
        fetchFilterValues,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'filePath',
                displayName: 'File Path',
                render: row => (
                    <Tooltip label={row.filePath} openDelay={500}>
                        <Code style={{maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap'}}>
                            {row.filePath}
                        </Code>
                    </Tooltip>
                ),
                width: '2fr',
            },
            {
                name: 'song',
                displayName: 'Song',
                render: row => <SessionRecordSong songInfo={row.songInfo} />,
                width: '2fr',
            },
            {
                name: 'action',
                displayName: 'Action',
                render: row => <Badge color={getActionColor(row.action)}>{row.action}</Badge>,
                width: 100,
            },
            {
                name: 'source',
                displayName: 'Source',
                render: row => <Text>{row.source}</Text>,
                width: 80,
            },
            {
                name: 'reason',
                displayName: 'Reason',
                render: row => row.reason ? (
                    <Tooltip label={row.reason} openDelay={500}>
                        <Text lineClamp={1} style={{maxWidth: '200px'}}>
                            {row.reason}
                        </Text>
                    </Tooltip>
                ) : <Text c="dimmed">-</Text>,
                width: '1.5fr',
            },
            {
                name: 'processedAt',
                displayName: 'Processed At',
                render: row => <Text>{row.processedAt ? formatDateTime(row.processedAt) : '-'}</Text>,
                width: '1fr',
            },
        ],

        estimateListRowHeight: () => 84,
        renderListArtwork: (_row, size) => <Artwork
            id={null}
            size={size}
            placeholderIcon={<IconFileMusic/>}
        />,
        renderListTitle: (row) => <Text fw={500} lineClamp={1}>{row.filePath.split('/').pop()}</Text>,
        renderListSubTitle: (row, lineClamp) => (
            <Text c="gray" size="sm" lineClamp={lineClamp}>
                {row.action} • {row.source}
            </Text>
        ),
    }) as CollectionSchema<RecordWithId>, [filterMetadata, fetchFilterValues]);
}
