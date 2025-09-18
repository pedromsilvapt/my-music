import {createFileRoute} from '@tanstack/react-router'
import AlbumsPage from "../components/albums/albums-page.tsx";

export const Route = createFileRoute('/albums')({
    component: Albums,
})

function Albums() {
    return <AlbumsPage/>;
}