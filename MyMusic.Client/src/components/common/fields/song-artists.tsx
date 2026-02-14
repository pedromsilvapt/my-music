import {Anchor, type DefaultMantineColor, Text, Tooltip} from "@mantine/core";
import {sepBy} from "../sep-by.tsx";

export interface SongArtistsProps {
    artists: {
        id?: string | number | undefined | null,
        name: string,
        link?: string | undefined | null,
    }[];
    c?: DefaultMantineColor;
}

export default function SongArtists(props: SongArtistsProps) {
    return sepBy(props.artists.map(((artist, i) => <>
        <Tooltip label={artist.name} openDelay={500}>
            <Anchor key={artist.id ?? i} href={artist.link ?? undefined} c={props.c ?? 'black'} inherit>
                {artist.name}
            </Anchor>
        </Tooltip>
    </>)), <Text inherit component="span">, </Text>);
}