import {Menu} from "@mantine/core";
import {useCallback, useEffect, useMemo, useState} from "react";

export interface UseMantineContextMenuOptions {
    menuWidth?: number;
    menuHeight?: number;
}

export interface UseMantineContextMenuReturn {
    onContextMenuTrigger: (event: React.MouseEvent | React.TouchEvent) => void;
    renderMenuItems: (children: () => React.ReactNode) => React.ReactNode;
    isOpen: boolean;
}

export function useMantineContextMenu(
    options: UseMantineContextMenuOptions = {}
): UseMantineContextMenuReturn {
    const {menuWidth = 200, menuHeight = 350} = options;
    const [position, setPosition] = useState<{ x: number; y: number } | null>(null);
    const [isOpen, setIsOpen] = useState(false);

    const onContextMenuTrigger = useCallback((event: React.MouseEvent | React.TouchEvent) => {
        event.preventDefault();

        let clientX: number, clientY: number;

        if ('touches' in event) {
            clientX = event.touches[0].clientX;
            clientY = event.touches[0].clientY;
        } else {
            clientX = event.clientX;
            clientY = event.clientY;
        }

        setPosition({x: clientX, y: clientY});
        setIsOpen(true);
    }, []);

    const onClose = useCallback(() => {
        setIsOpen(false);
        setPosition(null);
    }, []);

    useEffect(() => {
        if (!isOpen) return;

        const handleClick = () => {
            onClose();
        };

        document.addEventListener('click', handleClick);
        return () => document.removeEventListener('click', handleClick);
    }, [isOpen, onClose]);

    const constrainedPosition = useMemo(() => {
        if (!position) return null;
        return {
            left: Math.min(position.x, window.innerWidth - menuWidth),
            top: Math.min(position.y, window.innerHeight - menuHeight),
        };
    }, [position, menuWidth, menuHeight]);

    const renderMenuItems = useCallback((children: () => React.ReactNode) => {
        if (!constrainedPosition) return null;

        return (
            <Menu opened={isOpen} onClose={onClose}>
                <Menu.Dropdown
                    styles={{
                        dropdown: {
                            position: 'fixed',
                            left: constrainedPosition.left,
                            top: constrainedPosition.top,
                        },
                    }}
                >
                    {children()}
                </Menu.Dropdown>
            </Menu>
        );
    }, [constrainedPosition, isOpen, onClose]);

    return {
        onContextMenuTrigger,
        renderMenuItems,
        isOpen,
    };
}