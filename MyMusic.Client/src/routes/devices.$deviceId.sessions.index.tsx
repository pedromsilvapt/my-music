import {createFileRoute} from '@tanstack/react-router'
import DeviceSessionsPage from "../components/devices/device-sessions-page.tsx";

export const Route = createFileRoute('/devices/$deviceId/sessions/')({
    component: DeviceSessions,
})

function DeviceSessions() {
    return <DeviceSessionsPage/>;
}
