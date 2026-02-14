import {createFileRoute} from '@tanstack/react-router'
import ArtistDetailPage from "../components/artists/artist-detail-page.tsx";

export const Route = createFileRoute('/artists/$artistId')({
    component: ArtistDetail,
})

function ArtistDetail() {
    return <ArtistDetailPage/>;
}
