import {createFileRoute, Outlet} from '@tanstack/react-router'

export const Route = createFileRoute('/artists')({
    component: Artists,
})

function Artists() {
    return <Outlet/>;
}
