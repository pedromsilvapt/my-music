import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useGetDevices} from "../../client/devices.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {GetDevicesParams} from "../../model";
import Collection from "../common/collection/collection.tsx";
import {useDevicesSchema} from "./useDevicesSchema.tsx";

export default function DevicesPage() {
    const {t} = useTranslation(["devices", "common"]);
    const [appliedSearch, setAppliedSearch] = useState("");
    const [appliedFilter, setAppliedFilter] = useState("");

    const params: GetDevicesParams | undefined =
        appliedSearch || appliedFilter
            ? {search: appliedSearch || undefined, filter: appliedFilter || undefined}
            : undefined;

    const devicesQuery = useGetDevices(params);

    const devices = useQueryData(devicesQuery, t("devices:page.fetchFailed"));

    const devicesSchema = useDevicesSchema();

    const handleFilterChange = (newSearch: string, newFilter: string) => {
        setAppliedSearch(newSearch);
        setAppliedFilter(newFilter);
    };

    const elements = devices?.data?.devices ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}} data-testid="devices">
            <Collection
                key="devices"
                stateKey="devices"
                items={elements}
                schema={devicesSchema}
                isFetching={devicesQuery.isFetching}
                filterMode="server"
                serverSearch={appliedSearch}
                serverFilter={appliedFilter}
                onServerFilterChange={handleFilterChange}
                searchPlaceholder={t("devices:page.searchPlaceholder")}
            />
        </div>
    );
}
