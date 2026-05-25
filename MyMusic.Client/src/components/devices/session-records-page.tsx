import {useParams, Link} from "@tanstack/react-router";
import {useEffect, useState, useCallback, useMemo} from "react";
import {Anchor, Breadcrumbs, Text} from "@mantine/core";
import {useGetDevicesDeviceIdSessionsSessionIdRecords, useGetDevice} from "../../client/devices.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import {useSessionRecordsSchema} from "./useSessionRecordsSchema.tsx";
import {useDebouncedValue} from "@mantine/hooks";
import type {SyncRecordResponseItem} from "../../model";

const SEARCH_DEBOUNCE_MS = 300;

// Add id field to records for Collection component
type RecordWithId = Omit<SyncRecordResponseItem, 'id'> & { id: string };

export default function SessionRecordsPage() {
    const {deviceId, sessionId} = useParams({from: '/devices/$deviceId/sessions/$sessionId'});
    const deviceIdNum = parseInt(deviceId, 10);
    const sessionIdNum = parseInt(sessionId, 10);
    
    const deviceQuery = useGetDevice(deviceIdNum, {});
    const deviceResponse = useQueryData(deviceQuery, "Failed to fetch device");
    const device = deviceResponse?.data?.device;
    const deviceName = device?.name ?? `Device ${deviceId}`;
    
    const [searchQuery, setSearchQuery] = useState("");
    const [filterQuery, setFilterQuery] = useState("");
    const [debouncedSearch] = useDebouncedValue(searchQuery, SEARCH_DEBOUNCE_MS);
    const [debouncedFilter] = useDebouncedValue(filterQuery, SEARCH_DEBOUNCE_MS);
    
    // Build filter for file path search and advanced filter
    const filter = useMemo(() => {
        const parts: string[] = [];
        if (debouncedSearch) {
            parts.push(`filePath contains "${debouncedSearch}"`);
        }
        if (debouncedFilter) {
            parts.push(debouncedFilter);
        }
        return parts.length > 0 ? parts.join(' and ') : undefined;
    }, [debouncedSearch, debouncedFilter]);
    
    const recordsQuery = useGetDevicesDeviceIdSessionsSessionIdRecords(
        deviceIdNum, 
        sessionIdNum, 
        {
            includeSongInfo: true,
            filter: filter,
        }
    );
    
    const recordsResponse = useQueryData(recordsQuery, "Failed to fetch session records");
    const recordsSchema = useSessionRecordsSchema(deviceIdNum, sessionIdNum);
    
    const refetch = recordsQuery.refetch;
    
    useEffect(() => {
        refetch();
    }, [refetch]);
    
    // Transform records to add id field
    const records: RecordWithId[] = useMemo(() => {
        const rawRecords = recordsResponse?.data?.records ?? [];
        return rawRecords.map((record, index) => ({
            ...record,
            id: `${record.filePath}-${index}`,
        }));
    }, [recordsResponse?.data?.records]);
    
    const handleFilterChange = useCallback((search: string, filter: string) => {
        setSearchQuery(search);
        setFilterQuery(filter);
    }, []);
    
    const breadcrumbItems = [
        {title: 'Devices', href: '/devices', isLast: false},
        {title: deviceName, href: `/devices/${deviceId}/sessions`, isLast: false},
        {title: `Session ${sessionId}`, href: `/devices/${deviceId}/sessions/${sessionId}`, isLast: false},
        {title: 'Records', href: `/devices/${deviceId}/sessions/${sessionId}`, isLast: true},
    ];
    
    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <Breadcrumbs mb="md">
                {breadcrumbItems.map((item) => (
                    item.isLast ? (
                        <Text key="current" fw={500}>{item.title}</Text>
                    ) : (
                        <Anchor key={item.href} component={Link} to={item.href}>
                            {item.title}
                        </Anchor>
                    )
                ))}
            </Breadcrumbs>
            
            <div style={{flex: 1}}>
                <Collection
                    key={`session-records-${sessionId}`}
                    stateKey={`session-records-${sessionId}`}
                    items={records}
                    schema={recordsSchema}
                    initialView="table"
                    filterMode="server"
                    serverSearch={searchQuery}
                    serverFilter={filterQuery}
                    onServerFilterChange={handleFilterChange}
                    searchPlaceholder="Search file paths..."
                />
            </div>
        </div>
    );
}
