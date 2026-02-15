import {createFileRoute} from '@tanstack/react-router'
import PlaylistDetailPage from "../components/playlists/playlist-detail-page.tsx";

export const Route = createFileRoute('/playlists/$playlistId')({
    component: PlaylistDetail,
})

function PlaylistDetail() {
    return <PlaylistDetailPage/>;
}
