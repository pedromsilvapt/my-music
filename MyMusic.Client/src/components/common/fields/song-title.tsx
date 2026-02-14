import {Anchor, Tooltip} from "@mantine/core";
import ExplicitLabel from "../explicit-label.tsx";

export interface SongTitleProps {
    title: string;
    isExplicit?: boolean | undefined | null;
    link?: string | undefined | null;
    lineClamp?: number;
}

export default function SongTitle(props: SongTitleProps) {
    return <ExplicitLabel visible={props.isExplicit ?? false}>
        <Tooltip label={props.title} openDelay={500}>
            <Anchor lineClamp={props.lineClamp} c={"black"} href={props.link ?? undefined}>{props.title}</Anchor>
        </Tooltip>
    </ExplicitLabel>;
}