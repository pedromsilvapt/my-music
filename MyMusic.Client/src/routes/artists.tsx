import {createFileRoute} from '@tanstack/react-router'
import ArtistsPage from "../components/artists/artists-page.tsx";

export const Route = createFileRoute('/artists')({
    component: Artists,
})

function Artists() {
    return <ArtistsPage/>;
}