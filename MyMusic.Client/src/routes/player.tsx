import {createFileRoute} from '@tanstack/react-router'
import NowPlayingPage from "../components/player/now-playing-page.tsx";

export const Route = createFileRoute('/player')({
    component: NowPlaying,
})

function NowPlaying() {
    return <NowPlayingPage/>;
}
