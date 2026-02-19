import {createFileRoute} from '@tanstack/react-router'
import DevicesPage from "../components/devices/devices-page.tsx";

export const Route = createFileRoute('/devices/')({
    component: Devices,
})

function Devices() {
    return <DevicesPage/>;
}
