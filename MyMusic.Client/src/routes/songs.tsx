import {createFileRoute, Outlet} from '@tanstack/react-router'

export const Route = createFileRoute('/songs')({
    component: Songs,
})

function Songs() {
    return <Outlet/>;
}
