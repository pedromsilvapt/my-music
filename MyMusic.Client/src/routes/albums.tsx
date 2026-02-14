import {createFileRoute, Outlet} from '@tanstack/react-router'

export const Route = createFileRoute('/albums')({
    component: Albums,
})

function Albums() {
    return <Outlet/>;
}
