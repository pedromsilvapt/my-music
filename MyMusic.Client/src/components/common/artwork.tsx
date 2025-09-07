import {Image, ThemeIcon} from "@mantine/core";
import * as React from "react";
import { IconPhotoScan } from "@tabler/icons-react";

interface ArtworkProps {
    id: number | null | undefined;
    size?: number | undefined;
    placeholderIcon?: React.ReactNode | null | undefined; 
}

export default function Artwork(props: ArtworkProps) {
    const {id, size, placeholderIcon} = props;
    
    if (id == null) {
        return <ThemeIcon color="gray" size={32}>
            {placeholderIcon ?? <IconPhotoScan />}
        </ThemeIcon>;
    }
    
    let url = `/api/artwork/${id}`;
    
    if (size != null) {
        url += '?size=' + size;
    }
    
    return <Image
        radius="sm"
        h={size}
        w={size}
        src={url} />;
}