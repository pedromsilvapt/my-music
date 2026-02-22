import {type DefaultMantineColor, Text} from "@mantine/core";
import {sepBy} from "../sep-by.tsx";
import SongAlbum, {type SongAlbumProps} from "./song-album.tsx";
import SongArtists, {type SongArtistsProps} from "./song-artists.tsx";

export interface SongSubTitleProps {
    artists?: SongArtistsProps["artists"] | null | undefined;
    album?: SongAlbumProps | null | undefined;
    year?: number | null | undefined;
    c?: DefaultMantineColor;
}

export default function SongSubTitle(props: SongSubTitleProps) {
    const segments: React.ReactNode[] = [];

    if ((props.artists?.length ?? 0) > 0) {
        segments.push(<SongArtists c={props.c} artists={props.artists!}/>);
    }

    if (props.album != null) {
        segments.push(<SongAlbum c={props.c} name={props.album.name} albumId={props.album.albumId}
                                 link={props.album.link}/>);
    }

    if (props.year) {
        segments.push(<Text c={props.c} inherit component="span">{props.year}</Text>);
    }

    return <Text inherit component="span">
        {sepBy(segments, <Text c={props.c} inherit component="span"> • </Text>)}
    </Text>;
}