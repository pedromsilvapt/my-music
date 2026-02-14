import {Anchor, type DefaultMantineColor, Tooltip} from "@mantine/core";
import {Link} from "@tanstack/react-router";

export interface SongAlbumProps {
    name: string;
    albumId?: number | string;
    c?: DefaultMantineColor;
}

export default function SongAlbum(props: SongAlbumProps) {
    const content = (
        <Anchor component={Link} to={`/albums/${props.albumId}`} c={props.c ?? 'black'}>
            {props.name}
        </Anchor>
    );

    if (!props.albumId) {
        return <Tooltip label={props.name} openDelay={500}>
            <Anchor c={props.c ?? 'black'}>
                {props.name}
            </Anchor>
        </Tooltip>;
    }

    return <Tooltip label={props.name} openDelay={500}>
        {content}
    </Tooltip>;
}