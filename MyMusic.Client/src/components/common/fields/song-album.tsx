import {Anchor, type DefaultMantineColor, Tooltip} from "@mantine/core";
import {Link} from "@tanstack/react-router";

export interface SongAlbumProps {
    name: string;
    albumId?: number | string;
    link?: string | null;
    c?: DefaultMantineColor;
}

export default function SongAlbum(props: SongAlbumProps) {
    let content: React.ReactNode;

    if (props.link) {
        content = (
            <Anchor href={props.link} target="_blank" rel="noopener noreferrer" c={props.c ?? 'black'}>
                {props.name}
            </Anchor>
        );
    } else if (props.albumId) {
        content = (
            <Anchor component={Link} to={`/albums/${props.albumId}`} c={props.c ?? 'black'}>
                {props.name}
            </Anchor>
        );
    } else {
        content = (
            <Anchor c={props.c ?? 'black'}>
                {props.name}
            </Anchor>
        );
    }

    return <Tooltip label={props.name} openDelay={500}>
        {content}
    </Tooltip>;
}