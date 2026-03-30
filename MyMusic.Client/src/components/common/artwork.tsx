import {Box, Center, Image, Menu, Overlay, ThemeIcon} from "@mantine/core";
import {IconPhotoScan, IconPlayerPlayFilled, IconZoomIn} from "@tabler/icons-react";
import type {MouseEvent} from "react";
import * as React from "react";
import {useMemo} from "react";
import {DEFAULT_ARTWORK_SIZE} from "../../consts.ts";
import {useArtworkLightbox} from "../../contexts/artwork-lightbox-context.tsx";
import {useContextMenuTrigger} from "../../hooks/use-context-menu-trigger";
import styles from './artwork.module.css';
import {ContextMenuPortal} from "./context-menu-portal.tsx";

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
    const {openLightbox} = useArtworkLightbox();

    const contextMenuId = useMemo(() => `artwork-${id ?? 'no-id'}`, [id]);
    const {trigger: onContextMenuTrigger} = useContextMenuTrigger(contextMenuId);

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
        return <Box
            pos="relative"
            style={{cursor: "pointer"}}
            onClick={ev => {
                ev.stopPropagation();
                ev.preventDefault();
                if (fullSizeUrl) {
                    openLightbox(fullSizeUrl);
                }
            }}>
            {innerElement}
        </Box>;
    }

    const handleArtworkContextMenu = (event: React.MouseEvent) => {
        event.preventDefault();
        onContextMenuTrigger(event);
    };

    const handlePreview = () => {
        if (fullSizeUrl) {
            openLightbox(fullSizeUrl);
        }
    };

    return (
        <>
            <Box
                pos="relative"
                onContextMenu={handleArtworkContextMenu}
                data-artwork-preview
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
            <ContextMenuPortal 
                menuId={contextMenuId} 
                content={() => (
                    <Menu.Item
                        leftSection={<IconZoomIn size={16}/>}
                        onClick={handlePreview}
                    >
                        Preview
                    </Menu.Item>
                )}
            />
        </>
    );
}
