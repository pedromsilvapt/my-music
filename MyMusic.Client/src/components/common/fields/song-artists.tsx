import {Anchor, type DefaultMantineColor, Text, Tooltip} from "@mantine/core";
import {Link} from "@tanstack/react-router";
import {sepBy} from "../sep-by.tsx";

export interface SongArtistsProps {
    artists: {
        id?: number | string | undefined | null,
        name: string,
        link?: string | null,
    }[];
    c?: DefaultMantineColor;
}

export default function SongArtists(props: SongArtistsProps) {
    return sepBy(props.artists.map(((artist, i) => {
        let content: React.ReactNode;

        if (artist.link) {
            content = (
                <Anchor href={artist.link} target="_blank" rel="noopener noreferrer" key={artist.id ?? i}
                        c={props.c ?? 'black'} inherit>
                    {artist.name}
                </Anchor>
            );
        } else if (artist.id) {
            content = (
                <Anchor component={Link} to={`/artists/${artist.id}`} key={artist.id ?? i} c={props.c ?? 'black'}
                        inherit>
                    {artist.name}
                </Anchor>
            );
        } else {
            content = (
                <Anchor key={artist.id ?? i} c={props.c ?? 'black'} inherit>
                    {artist.name}
                </Anchor>
            );
        }

        return (
            <Tooltip key={artist.id ?? i} label={artist.name} openDelay={500}>
                {content}
            </Tooltip>
        );
    })), <Text inherit component="span">, </Text>);
}