import {ActionIcon, Menu, useComputedColorScheme} from "@mantine/core";
import {IconDeviceDesktop, IconMoon, IconSun} from "@tabler/icons-react";

import {useUserPreferences} from "../../hooks/use-user-preferences";

export default function ThemeToggle() {
    const {colorScheme, updateColorScheme, isUpdating} = useUserPreferences();
    const computedColorScheme = useComputedColorScheme("light");

    const iconProps = {size: 18, stroke: 1.5};

    return (
        <Menu position="bottom-end" withinPortal>
            <Menu.Target>
                <ActionIcon
                    variant="default"
                    size="lg"
                    aria-label="Toggle color scheme"
                    loading={isUpdating}
                >
                    {computedColorScheme === "dark" ? (
                        <IconMoon {...iconProps} />
                    ) : (
                        <IconSun {...iconProps} />
                    )}
                </ActionIcon>
            </Menu.Target>
            <Menu.Dropdown>
                <Menu.Item
                    leftSection={<IconSun {...iconProps} />}
                    onClick={() => updateColorScheme("light")}
                    disabled={colorScheme === "light"}
                >
                    Light
                </Menu.Item>
                <Menu.Item
                    leftSection={<IconMoon {...iconProps} />}
                    onClick={() => updateColorScheme("dark")}
                    disabled={colorScheme === "dark"}
                >
                    Dark
                </Menu.Item>
                <Menu.Item
                    leftSection={<IconDeviceDesktop {...iconProps} />}
                    onClick={() => updateColorScheme("auto")}
                    disabled={colorScheme === "auto"}
                >
                    System
                </Menu.Item>
            </Menu.Dropdown>
        </Menu>
    );
}