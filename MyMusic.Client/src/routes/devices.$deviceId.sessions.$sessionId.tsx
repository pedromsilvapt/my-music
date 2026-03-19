import {createFileRoute} from '@tanstack/react-router'
import SessionRecordsPage from "../components/devices/session-records-page.tsx";

export const Route = createFileRoute('/devices/$deviceId/sessions/$sessionId')({
    component: SessionRecords,
})

function SessionRecords() {
    return <SessionRecordsPage/>;
}
