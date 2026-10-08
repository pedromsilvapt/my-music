import {useParams} from "@tanstack/react-router";
import {useEffect, useState, useCallback, useMemo} from "react";
import {useTranslation} from "react-i18next";
import {useGetDevice} from "../../client/devices.ts";
import {useGetDevicesDeviceIdSessionsSessionId, useGetDevicesDeviceIdSessionsSessionIdRecords} from "../../client/device-sync-sessions.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import Collection from "../common/collection/collection.tsx";
import DeviceBreadcrumbs from "./device-breadcrumbs.tsx";
import {useSessionRecordsSchema} from "./useSessionRecordsSchema.tsx";
import SessionActionPills from "./session-action-pills.tsx";
import {useDebouncedValue} from "@mantine/hooks";
import type {SyncRecordResponseItem} from "../../model";

const SEARCH_DEBOUNCE_MS = 300;

// Add id field to records for Collection component
type RecordWithId = Omit<SyncRecordResponseItem, 'id'> & { id: string };

export default function SessionRecordsPage() {
    const {t} = useTranslation(["devices", "common"]);
    const {deviceId, sessionId} = useParams({from: '/devices/$deviceId/sessions/$sessionId'});
    const deviceIdNum = parseInt(deviceId, 10);
    const sessionIdNum = parseInt(sessionId, 10);
    
    const deviceQuery = useGetDevice(deviceIdNum, {});
    const deviceResponse = useQueryData(deviceQuery, t("devices:recordsPage.fetchDeviceFailed"));
    const device = deviceResponse?.data?.device;

    const sessionQuery = useGetDevicesDeviceIdSessionsSessionId(deviceIdNum, sessionIdNum);
    const sessionResponse = useQueryData(sessionQuery, t("devices:recordsPage.fetchSessionFailed"));
    const session = sessionResponse?.data?.session;
    
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
    
    const recordsResponse = useQueryData(recordsQuery, t("devices:recordsPage.fetchFailed"));
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
    
    return (
        <div style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}>
            <DeviceBreadcrumbs
                deviceId={deviceId}
                deviceName={device?.name}
                items={[
                    {title: t("devices:recordsPage.session", {id: sessionId}), href: `/devices/${deviceId}/sessions/${sessionId}`},
                    {title: t("devices:recordsPage.records")},
                ]}
            />

            <SessionActionPills
                session={session}
                filter={filterQuery}
                onFilterChange={setFilterQuery}
            />
            
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
                    searchPlaceholder={t("devices:recordsPage.searchPlaceholder")}
                />
            </div>
        </div>
    );
}
