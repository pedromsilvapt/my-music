import {Badge, Group, parseThemeColor, Text, Tooltip, useMantineTheme} from "@mantine/core";
import {IconPointFilled} from "@tabler/icons-react";
import TablerIcon from "../common/tabler-icon.tsx";

interface DeviceBadgeProps {
    name: string;
    icon?: string | null;
    color?: string | null;
    syncAction?: "Download" | "Remove" | string | null;
    showTooltip?: boolean;
}

export default function DeviceBadge({name, icon, color, syncAction, showTooltip = true}: DeviceBadgeProps) {
    const theme = useMantineTheme();

    const badgeColor = color || 'gray';
    const badgeThemeColor = parseThemeColor({color: badgeColor, theme});

    const iconColor = badgeThemeColor.isLight ? 'black' : 'white';
    const actionColor = getActionColor(syncAction, badgeThemeColor.isLight);

    const tooltipLabel = syncAction
        ? `${name} - To ${syncAction === 'Remove' ? 'Delete' : syncAction}`
        : name;

    const badge = (
        <Badge color={badgeColor}>
            <Group gap={4}>
                {icon && <TablerIcon icon={icon} size={12} color={iconColor}/>}
                <Text inherit c={iconColor}>{name}</Text>
                {syncAction && actionColor && (
                    <IconPointFilled size={12} color={actionColor}/>
                )}
            </Group>
        </Badge>
    );

    if (showTooltip) {
        return (
            <Tooltip label={tooltipLabel}>
                {badge}
            </Tooltip>
        );
    }

    return badge;
}

function getActionColor(syncAction: DeviceBadgeProps["syncAction"], isLightColor: boolean) {
    return syncAction === 'Download' ? (isLightColor ? 'var(--mantine-color-green-9)' : 'var(--mantine-color-green-3)') :
        syncAction === 'Remove' ? (isLightColor ? 'red' : 'var(--mantine-color-red-3)') :
            undefined;
}