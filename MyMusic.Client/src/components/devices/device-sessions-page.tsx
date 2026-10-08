import {useParams} from "@tanstack/react-router";
import {useTranslation} from "react-i18next";
import {useGetDevice} from "../../client/devices.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import DeviceBreadcrumbs from "./device-breadcrumbs.tsx";
import DeviceSessionsCollection from "./device-sessions-collection.tsx";

export default function DeviceSessionsPage() {
    const {t} = useTranslation(["devices", "common"]);
    const {deviceId} = useParams({from: '/devices/$deviceId/sessions/'});
    const deviceIdNum = parseInt(deviceId, 10);

    const deviceQuery = useGetDevice(deviceIdNum, {});
    const deviceResponse = useQueryData(deviceQuery, t("devices:sessionsPage.fetchDeviceFailed"));
    const device = deviceResponse?.data?.device;

    return (
        <div
            style={{height: 'var(--parent-height)', display: 'flex', flexDirection: 'column'}}
            data-testid="device-sessions"
            data-loading={deviceQuery.isFetching ? "true" : "false"}
        >
            <DeviceBreadcrumbs
                deviceId={deviceId}
                deviceName={device?.name}
                items={[{title: t("devices:sessionsPage.sessions")}]}
            />

            <div style={{flex: 1}}>
                <DeviceSessionsCollection deviceId={deviceIdNum}/>
            </div>
        </div>
    );
}
