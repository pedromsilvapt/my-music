import {createFileRoute} from '@tanstack/react-router'
import AuditsPage from "../components/audits/audits-page.tsx";

export const Route = createFileRoute('/audits/')({
    component: Audits,
})

function Audits() {
    return <AuditsPage/>;
}
