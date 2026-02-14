import {IconMusic} from "@tabler/icons-react";
import Artwork from "../artwork.tsx";

export interface SongArtworkProps {
    id?: number | null | undefined;
    url?: string | null | undefined;
    onClick?: ((ev: React.MouseEvent<Element, MouseEvent>) => void) | null | undefined;
    size?: number | null | undefined;
}

export default function SongArtwork(props: SongArtworkProps) {
    return <Artwork
        id={props.id}
        url={props.url}
        size={props.size ?? 32}
        placeholderIcon={<IconMusic/>}
        onClick={props.onClick ?? undefined}
    />;
}