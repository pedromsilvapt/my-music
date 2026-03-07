import type {IconProps} from "@tabler/icons-react";
import {useEffect, useState} from "react"
import {DEFAULT_ICON_SIZE, DEFAULT_ICON_STROKE} from "../../consts.ts";

const getIconPath = (iconName: string) => `../../../node_modules/@tabler/icons-react/dist/esm/icons/${iconName}.mjs`;
const iconModules = import.meta.glob(`../../../node_modules/@tabler/icons-react/dist/esm/icons/*.mjs`)

const FALLBACK_ICON = "IconHelpSquareFilled";

export interface TablerIconProps extends IconProps {
    icon: string | null | undefined;
    defaultIcon?: string;
}

export default function TablerIcon({
                                       icon,
                                       defaultIcon,
                                       color = "gray",
                                       size = DEFAULT_ICON_SIZE,
                                       stroke = DEFAULT_ICON_STROKE,
                                   }: TablerIconProps) {
    const [Icon, setIcon] = useState<any>(null)

    useEffect(() => {
        let mounted = true;

        const iconPath = getIconPath(icon ?? '');
        const module = iconModules[iconPath];

        if (module) {
            module().then((mod: any) => {
                if (mounted) setIcon(() => mod.default)
            })
        } else if (defaultIcon) {
            const fallbackPath = getIconPath(defaultIcon);
            const fallbackModule = iconModules[fallbackPath];
            if (fallbackModule) {
                fallbackModule().then((mod: any) => {
                    if (mounted) setIcon(() => mod.default)
                })
            } else {
                loadFallbackIcon(mounted);
            }
        } else {
            loadFallbackIcon(mounted);
        }

        function loadFallbackIcon(mounted: boolean) {
            const fallbackPath = getIconPath(FALLBACK_ICON);
            const fallbackModule = iconModules[fallbackPath];
            if (fallbackModule) {
                fallbackModule().then((mod: any) => {
                    if (mounted) setIcon(() => mod.default)
                })
            }
        }

        return () => {
            mounted = false;
        }
    }, [icon, defaultIcon])

    if (!Icon) {
        return null;
    }

    return <Icon color={color} size={size} stroke={stroke}/>;
}
