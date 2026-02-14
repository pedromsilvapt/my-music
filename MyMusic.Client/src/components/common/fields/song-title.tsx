import {Anchor, Tooltip} from "@mantine/core";
import {Link} from "@tanstack/react-router";
import ExplicitLabel from "../explicit-label.tsx";

export interface SongTitleProps {
    title: string;
    songId?: number | string;
    isExplicit?: boolean | undefined | null;
    lineClamp?: number;
}

export default function SongTitle(props: SongTitleProps) {
    const content = (
        <Anchor component={Link} to={`/songs/${props.songId}`} lineClamp={props.lineClamp} c={"black"}>
            {props.title}
        </Anchor>
    );

    return <ExplicitLabel visible={props.isExplicit ?? false}>
        <Tooltip label={props.title} openDelay={500}>
            {props.songId ? content : <Anchor lineClamp={props.lineClamp} c={"black"}>{props.title}</Anchor>}
        </Tooltip>
    </ExplicitLabel>;
}