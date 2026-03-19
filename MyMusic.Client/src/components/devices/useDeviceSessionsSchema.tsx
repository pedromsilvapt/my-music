import {useMemo} from "react";
import {Link} from "@tanstack/react-router";
import {Anchor, Badge, Code, Text, Tooltip} from "@mantine/core";
import {IconTrash, IconHistory} from "@tabler/icons-react";
import type {SyncSessionItem} from "../../model";
import type {CollectionSchema} from "../common/collection/collection.tsx";
import Artwork from "../common/artwork.tsx";

function formatDateTime(date: string | Date): string {
    const d = new Date(date);
    return d.toLocaleString();
}

function computeDuration(session: SyncSessionItem): string {
    if (session.status === 'InProgress') {
        return "In Progress";
    }
    if (!session.completedAt) {
        return "-";
    }
    const start = new Date(session.startedAt).getTime();
    const end = new Date(session.completedAt).getTime();
    const diff = end - start;
    const minutes = Math.floor(diff / 60000);
    const seconds = Math.floor((diff % 60000) / 1000);
    if (minutes > 0) {
        return `${minutes}m ${seconds}s`;
    }
    return `${seconds}s`;
}

function getStatusColor(status: string): string {
    switch (status) {
        case 'Completed':
            return 'green';
        case 'InProgress':
            return 'yellow';
        case 'Cancelled':
            return 'red';
        default:
            return 'gray';
    }
}

function inlineCounters({session}: { session: SyncSessionItem }) {
    const counters = [
        {label: 'C', value: session.createdCount, color: 'green'},
        {label: 'U', value: session.updatedCount, color: 'blue'},
        {label: 'S', value: session.skippedCount, color: 'gray'},
        {label: 'D', value: session.downloadedCount, color: 'cyan'},
        {label: 'R', value: session.removedCount, color: 'orange'},
        {label: 'E', value: session.errorCount, color: 'red'},
    ];

    return (
        <div style={{display: 'flex', gap: '4px'}}>
            {counters.map(c => (
                c.value > 0 && (
                    <Badge key={c.label} size="xs" color={c.color} variant="light">
                        {c.label}{c.value}
                    </Badge>
                )
            ))}
        </div>
    );
}

function renderCounter(value: number, color: string) {
    return value > 0 ? (
        <Text c={color} fw={500} ta="center">{value}</Text>
    ) : (
        <Text c="dimmed" ta="center">-</Text>
    );
}

export function useDeviceSessionsSchema(deviceId: number) {
    return useMemo(() => ({
        key: row => row.id,
        searchVector: session => `Session ${session.id}`,

        estimateTableRowHeight: () => 47 * 2,
        columns: [
            {
                name: 'id',
                displayName: 'ID',
                render: row => (
                    <Anchor component={Link} to={`/devices/${deviceId}/sessions/${row.id}`}>
                        #{row.id}
                    </Anchor>
                ),
                width: 80,
            },
            {
                name: 'startedAt',
                displayName: 'Started At',
                render: row => <Text>{formatDateTime(row.startedAt)}</Text>,
                width: '1.5fr',
                sortable: true,
            },
            {
                name: 'duration',
                displayName: 'Duration',
                render: row => <Text>{computeDuration(row)}</Text>,
                width: 100,
            },
            {
                name: 'status',
                displayName: 'Status',
                render: row => <Badge color={getStatusColor(row.status)}>{row.status}</Badge>,
                width: 120,
            },
            {
                name: 'isDryRun',
                displayName: 'Dry Run',
                render: row => row.isDryRun ? <Badge color="gray">Yes</Badge> : <Text c="dimmed">No</Text>,
                width: 90,
                align: 'center',
            },
            {
                name: 'createdCount',
                displayName: 'Created',
                render: row => renderCounter(row.createdCount, 'green'),
                width: 70,
                align: 'center',
            },
            {
                name: 'updatedCount',
                displayName: 'Updated',
                render: row => renderCounter(row.updatedCount, 'blue'),
                width: 70,
                align: 'center',
            },
            {
                name: 'skippedCount',
                displayName: 'Skipped',
                render: row => renderCounter(row.skippedCount, 'gray'),
                width: 70,
                align: 'center',
            },
            {
                name: 'downloadedCount',
                displayName: 'Downloaded',
                render: row => renderCounter(row.downloadedCount, 'cyan'),
                width: 85,
                align: 'center',
            },
            {
                name: 'removedCount',
                displayName: 'Removed',
                render: row => renderCounter(row.removedCount, 'orange'),
                width: 75,
                align: 'center',
            },
            {
                name: 'errorCount',
                displayName: 'Errors',
                render: row => renderCounter(row.errorCount, 'red'),
                width: 65,
                align: 'center',
            },
            {
                name: 'repositoryPath',
                displayName: 'Repository Path',
                render: row => (
                    <Tooltip label={row.repositoryPath || '-'} openDelay={500}>
                        <Code style={{maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap'}}>
                            {row.repositoryPath || '-'}
                        </Code>
                    </Tooltip>
                ),
                width: '2fr',
            },
        ],

        actions: () => {
            return [
                {group: "Manage"},
                {
                    name: "delete",
                    renderIcon: () => <IconTrash/>,
                    renderLabel: () => "Delete",
                    onClick: (_sessions: SyncSessionItem[]) => {
                        // This will be handled by the component that has access to deviceId
                    },
                }
            ];
        },

        estimateListRowHeight: () => 100,
        renderListArtwork: (_row, size) => <Artwork
            id={null}
            size={size}
            placeholderIcon={<IconHistory/>}
        />,
        renderListTitle: (row) => (
            <Anchor component={Link} to={`/devices/${deviceId}/sessions/${row.id}`}>
                <Text fw={500}>Session #{row.id}</Text>
            </Anchor>
        ),
        renderListSubTitle: (row, lineClamp) => (
            <div>
                <Text c="gray" size="sm" lineClamp={lineClamp}>
                    {formatDateTime(row.startedAt)} • {computeDuration(row)}
                </Text>
                <div style={{marginTop: '4px', display: 'flex', gap: '4px', flexWrap: 'wrap'}}>
                    {inlineCounters({session: row})}
                </div>
            </div>
        ),
    }) as CollectionSchema<SyncSessionItem>, [deviceId]);
}
