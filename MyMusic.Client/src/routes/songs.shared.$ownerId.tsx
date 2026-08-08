import {createFileRoute} from '@tanstack/react-router'
import SongsPage from "../components/songs/songs-page.tsx";
import {useSharers} from "../hooks/use-sharers";

export const Route = createFileRoute('/songs/shared/$ownerId')({
    component: SharedSongs,
})

function SharedSongs() {
    const {ownerId} = Route.useParams();
    const {sharers} = useSharers();
    const sharer = sharers.find(s => s.id === Number(ownerId));
    const numericOwnerId = Number(ownerId);

    return <SongsPage ownerId={numericOwnerId} sharerName={sharer?.name}/>;
}