import {Anchor, type DefaultMantineColor, Tooltip} from "@mantine/core";

export interface SongAlbumProps {
    name: string;
    link?: string | null | undefined;
    c?: DefaultMantineColor;
}

export default function SongAlbum(props: SongAlbumProps) {
    return <Tooltip label={props.name} openDelay={500}>
        <Anchor c={props.c ?? 'black'} href={props.link ?? undefined}>{props.name}</Anchor>
    </Tooltip>;
}