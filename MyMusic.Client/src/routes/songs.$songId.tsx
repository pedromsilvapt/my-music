import {createFileRoute} from '@tanstack/react-router'
import SongDetailPage from "../components/songs/song-detail-page.tsx";

export const Route = createFileRoute('/songs/$songId')({
    component: SongDetail,
})

function SongDetail() {
    return <SongDetailPage/>;
}
