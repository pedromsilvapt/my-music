import {Box, Center, Image, Overlay, ThemeIcon} from "@mantine/core";
import type {MouseEvent} from "react";
import * as React from "react";
import {IconPhotoScan, IconPlayerPlayFilled} from "@tabler/icons-react";
import styles from './artwork.module.css';

interface ArtworkProps {
    id: number | null | undefined;
    size?: number | undefined;
    placeholderIcon?: React.ReactNode | null | undefined;
    onClick?: (ev: MouseEvent) => void | null;
}

export default function Artwork(props: ArtworkProps) {
    let {id, size, placeholderIcon} = props;

    size ??= 32;

    let innerElement: React.ReactNode;

    if (id == null) {
        innerElement = <ThemeIcon color="gray" size={size}>
            {placeholderIcon ?? <IconPhotoScan/>}
        </ThemeIcon>;
    } else {
        let url = `/api/artwork/${id}?size=${size}`;

        innerElement = <Image
            radius="sm"
            h={size}
            w={size}
            src={url}/>;
    }

    const onClick = props.onClick;

    if (!onClick) {
        return innerElement;
    }

    return <Box pos="relative">
        {innerElement}
        <Overlay
            className={styles.overlay}
            color="#000"
            backgroundOpacity={0.35}
            onClick={ev => onClick(ev)}
        >
            <Center maw="100%" h="100%">
                <IconPlayerPlayFilled color="white" size={size * 0.6}/>
            </Center>
        </Overlay>
    </Box>;
}