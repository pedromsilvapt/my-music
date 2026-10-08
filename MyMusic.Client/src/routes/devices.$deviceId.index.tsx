import {createFileRoute} from '@tanstack/react-router'
import DeviceDetailPage from "../components/devices/device-detail-page.tsx";

export const Route = createFileRoute('/devices/$deviceId/')({
    component: DeviceDetail,
})

function DeviceDetail() {
    return <DeviceDetailPage/>;
}
