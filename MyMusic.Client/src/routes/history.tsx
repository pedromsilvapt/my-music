import {createFileRoute} from '@tanstack/react-router'
import HistoryPage from "../components/history/history-page.tsx";

export const Route = createFileRoute('/history')({
    component: History,
})

function History() {
    return <HistoryPage/>;
}
