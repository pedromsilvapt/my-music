import {createFileRoute} from '@tanstack/react-router'
import PlaylistsPage from "../components/playlists/playlists-page.tsx";

export const Route = createFileRoute('/playlists/')({
    component: Playlists,
})

function Playlists() {
    return <PlaylistsPage/>;
}
