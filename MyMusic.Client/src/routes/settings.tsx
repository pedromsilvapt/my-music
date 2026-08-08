import {createFileRoute} from '@tanstack/react-router'
import SettingsPage from "../components/settings/settings-page.tsx";

export const Route = createFileRoute('/settings')({
    component: Settings,
})

function Settings() {
    return <SettingsPage/>;
}