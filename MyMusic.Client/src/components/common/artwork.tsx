import {Box, Center, Image, Overlay, ThemeIcon} from "@mantine/core";
import {IconPhotoScan, IconPlayerPlayFilled, IconZoomIn} from "@tabler/icons-react";
import {useContextMenu} from "mantine-contextmenu";
import type {MouseEvent} from "react";
import * as React from "react";
import {useState} from "react";
import {DEFAULT_ARTWORK_SIZE} from "../../consts.ts";
import ArtworkLightbox from "./artwork-lightbox.tsx";
import styles from './artwork.module.css';

interface ArtworkProps {
    id?: number | null | undefined;
    url?: string | null | undefined;
    size?: number | undefined;
    placeholderIcon?: React.ReactNode | null | undefined;
    onClick?: (ev: MouseEvent) => void | null;
    enablePreview?: boolean;
}

export default function Artwork(props: ArtworkProps) {
    const {id, placeholderIcon, enablePreview = true} = props;
    const size = props.size ?? DEFAULT_ARTWORK_SIZE;
    const [lightboxOpened, setLightboxOpened] = useState(false);
    const {showContextMenu} = useContextMenu();

    const hasArtwork = id != null || props.url != null;

    const getFullSizeUrl = () => {
        if (props.url != null) {
            return props.url;
        }
        if (id != null) {
            return `/api/artwork/${id}`;
        }
        return null;
    };

    const fullSizeUrl = getFullSizeUrl();

    let innerElement: React.ReactNode;

    if (!hasArtwork) {
        innerElement = <ThemeIcon color="gray" size={size}>
            {placeholderIcon ?? <IconPhotoScan/>}
        </ThemeIcon>;
    } else {
        const url = props.url ?? `/api/artwork/${id}?size=${size}`;

        innerElement = <Image
            radius="sm"
            h={size}
            w={size}
            src={url}/>;
    }

    const onClick = props.onClick;

    if (!hasArtwork || !enablePreview) {
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

    if (!onClick) {
        return <>
            <Box
                pos="relative"
                style={{cursor: "pointer"}}
                onClick={() => setLightboxOpened(true)}
            >
                {innerElement}
            </Box>
            {fullSizeUrl && (
                <ArtworkLightbox
                    opened={lightboxOpened}
                    onClose={() => setLightboxOpened(false)}
                    src={fullSizeUrl}
                />
            )}
        </>;
    }

    const handleContextMenu = showContextMenu([
        {
            key: "preview",
            icon: <IconZoomIn size={16}/>,
            title: "Preview",
            onClick: () => setLightboxOpened(true)
        }
    ]);

    return <>
        <Box
            pos="relative"
            onContextMenu={handleContextMenu}
        >
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
        </Box>
        {fullSizeUrl && (
            <ArtworkLightbox
                opened={lightboxOpened}
                onClose={() => setLightboxOpened(false)}
                src={fullSizeUrl}
            />
        )}
    </>;
}
