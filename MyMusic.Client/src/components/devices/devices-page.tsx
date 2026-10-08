import {ActionIcon} from "@mantine/core";
import {IconPlus} from "@tabler/icons-react";
import {useNavigate} from "@tanstack/react-router";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {useGetDevices} from "../../client/devices.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {GetDevicesParams} from "../../model";
import Collection from "../common/collection/collection.tsx";
import CollectionToolbar from "../common/collection/collection-toolbar.tsx";
import {useCreateDevice} from "./use-create-device.ts";
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
    const createDevice = useCreateDevice();
    const navigate = useNavigate();

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
                toolbar={p => (
                    <CollectionToolbar
                        {...p}
                        renderExtraActions={() => (
                            <ActionIcon
                                variant="default"
                                size="lg"
                                aria-label={t("devices:page.createDevice")}
                                title={t("devices:page.createDevice")}
                                onClick={() => createDevice(deviceId => navigate({
                                    to: '/devices/$deviceId',
                                    params: {deviceId: String(deviceId)},
                                }))}
                                data-testid="create-device"
                            >
                                <IconPlus/>
                            </ActionIcon>
                        )}
                    />
                )}
            />
        </div>
    );
}
