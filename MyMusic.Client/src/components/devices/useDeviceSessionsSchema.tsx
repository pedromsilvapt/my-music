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
        {label: 'CR', value: session.createRemoteCount, color: 'green'},
        {label: 'UR', value: session.updateRemoteCount, color: 'blue'},
        {label: 'S', value: session.skippedCount, color: 'gray'},
        {label: 'CL', value: session.createLocalCount, color: 'teal'},
        {label: 'UL', value: session.updateLocalCount, color: 'cyan'},
        {label: 'Del', value: session.deleteCount, color: 'red'},
        {label: 'Lnk', value: session.linkCount, color: 'lime'},
        {label: 'Unl', value: session.unlinkCount, color: 'orange'},
        {label: 'Rnm', value: session.renameCount, color: 'violet'},
        {label: 'Cfl', value: session.conflictCount, color: 'yellow'},
        {label: 'UT', value: session.updateTimestampCount, color: 'grape'},
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
                render: row => row.isDryRun ? <Badge color="yellow">Yes</Badge> : <Text c="dimmed">No</Text>,
                width: 90,
                align: 'center',
            },
            {
                name: 'createRemoteCount',
                displayName: 'CrtRem',
                render: row => renderCounter(row.createRemoteCount, 'green'),
                width: 65,
                align: 'center',
            },
            {
                name: 'updateRemoteCount',
                displayName: 'UpdRem',
                render: row => renderCounter(row.updateRemoteCount, 'blue'),
                width: 65,
                align: 'center',
            },
            {
                name: 'skippedCount',
                displayName: 'Skipped',
                render: row => renderCounter(row.skippedCount, 'gray'),
                width: 65,
                align: 'center',
            },
            {
                name: 'createLocalCount',
                displayName: 'CrtLoc',
                render: row => renderCounter(row.createLocalCount, 'teal'),
                width: 65,
                align: 'center',
            },
            {
                name: 'updateLocalCount',
                displayName: 'UpdLoc',
                render: row => renderCounter(row.updateLocalCount, 'cyan'),
                width: 65,
                align: 'center',
            },
            {
                name: 'deleteCount',
                displayName: 'Delete',
                render: row => renderCounter(row.deleteCount, 'red'),
                width: 60,
                align: 'center',
            },
            {
                name: 'linkCount',
                displayName: 'Link',
                render: row => renderCounter(row.linkCount, 'lime'),
                width: 55,
                align: 'center',
            },
            {
                name: 'unlinkCount',
                displayName: 'Unlink',
                render: row => renderCounter(row.unlinkCount, 'orange'),
                width: 60,
                align: 'center',
            },
            {
                name: 'renameCount',
                displayName: 'Rename',
                render: row => renderCounter(row.renameCount, 'violet'),
                width: 65,
                align: 'center',
            },
            {
                name: 'conflictCount',
                displayName: 'Conflicts',
                render: row => renderCounter(row.conflictCount, 'yellow'),
                width: 70,
                align: 'center',
            },
            {
                name: 'updateTimestampCount',
                displayName: 'UpdTs',
                render: row => renderCounter(row.updateTimestampCount, 'grape'),
                width: 60,
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
