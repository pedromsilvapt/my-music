import {Anchor, Tooltip} from "@mantine/core";
import {IconPlayerPlayFilled} from "@tabler/icons-react";
import {Link} from "@tanstack/react-router";
import ExplicitLabel from "../explicit-label.tsx";

export interface SongTitleProps {
    title: string;
    songId?: number | string;
    link?: string | null;
    isExplicit?: boolean | undefined | null;
    lineClamp?: number;
    isPlaying?: boolean;
}

export default function SongTitle(props: SongTitleProps) {
    let content: React.ReactNode;

    if (props.link) {
        content = <Anchor href={props.link} target="_blank" rel="noopener noreferrer" lineClamp={props.lineClamp}
                          c={"black"}>{props.title}</Anchor>;
    } else if (props.songId) {
        content = <Anchor component={Link} to={`/songs/${props.songId}`} lineClamp={props.lineClamp}
                          c={"black"}>{props.title}</Anchor>;
    } else {
        content = <Anchor lineClamp={props.lineClamp} c={"black"}>{props.title}</Anchor>;
    }

    return <ExplicitLabel visible={props.isExplicit ?? false}>
        {props.isPlaying &&
            <IconPlayerPlayFilled size={16} style={{marginRight: 4}} color="var(--mantine-primary-color-3)"/>}
        <Tooltip label={props.title} openDelay={500}>
            {content}
        </Tooltip>
    </ExplicitLabel>;
}