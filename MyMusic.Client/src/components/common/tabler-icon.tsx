import type {IconProps} from "@tabler/icons-react";
import {useEffect, useState} from "react"
import {DEFAULT_ICON_SIZE, DEFAULT_ICON_STROKE} from "../../consts.ts";

const getIconPath = (iconName: string) => `../../../node_modules/@tabler/icons-react/dist/esm/icons/${iconName}.mjs`;
const iconModules = import.meta.glob(`../../../node_modules/@tabler/icons-react/dist/esm/icons/*.mjs`)

export interface TablerIconProps extends IconProps {
    icon: string
}

export default function TablerIcon({
                                       icon,
                                       color = "gray",
                                       size = DEFAULT_ICON_SIZE,
                                       stroke = DEFAULT_ICON_STROKE,
                                   }: TablerIconProps) {
    const [Icon, setIcon] = useState<any>(null)

    useEffect(() => {
        let mounted = true;

        iconModules[getIconPath(icon)]().then((mod: any) => {
            if (mounted) setIcon(() => mod.default)
        })

        return () => {
            mounted = false;
        }
    }, [icon])

    if (!Icon) {
        return null;
    }

    return <Icon color={color} size={size} stroke={stroke}/>;
}