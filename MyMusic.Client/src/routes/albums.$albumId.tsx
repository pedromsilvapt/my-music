import {createFileRoute} from '@tanstack/react-router'
import AlbumDetailPage from "../components/albums/album-detail-page.tsx";

export const Route = createFileRoute('/albums/$albumId')({
    component: AlbumDetail,
})

function AlbumDetail() {
    return <AlbumDetailPage/>;
}
