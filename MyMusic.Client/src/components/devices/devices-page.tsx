import {useGetApiDevices} from "../../client/devices.ts";
import Collection from "../common/collection/collection.tsx";
import {useDevicesSchema} from "./useDevicesSchema.tsx";

export default function DevicesPage() {
    const {data: devices} = useGetApiDevices();
    const devicesSchema = useDevicesSchema();

    const elements = devices?.data?.devices ?? [];

    return (
        <div style={{height: 'var(--parent-height)'}}>
            <Collection
                key="devices"
                items={elements}
                schema={devicesSchema}>
            </Collection>
        </div>
    );
}
