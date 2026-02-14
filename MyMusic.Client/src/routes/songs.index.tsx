import {createFileRoute} from '@tanstack/react-router'
import SongsPage from "../components/songs/songs-page.tsx";

export const Route = createFileRoute('/songs/')({
    component: Songs,
})

function Songs() {
    return <SongsPage/>;
}