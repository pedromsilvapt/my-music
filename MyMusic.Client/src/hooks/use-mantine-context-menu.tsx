import {Menu} from "@mantine/core";
import {useElementSize, useViewportSize} from "@mantine/hooks";
import {useCallback, useEffect, useMemo, useState} from "react";

export interface UseMantineContextMenuReturn {
    onContextMenuTrigger: (event: React.MouseEvent | React.TouchEvent) => void;
    renderMenuItems: (children: () => React.ReactNode) => React.ReactNode;
    isOpen: boolean;
}

export function useMantineContextMenu(): UseMantineContextMenuReturn {
    const {ref: menuRef, width, height} = useElementSize<HTMLDivElement>();
    const {width: viewportWidth, height: viewportHeight} = useViewportSize();
    const [clickPosition, setClickPosition] = useState<{ x: number; y: number } | null>(null);
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

        setClickPosition({x: clientX, y: clientY});
        setIsOpen(true);
    }, []);

    const onClose = useCallback(() => {
        setIsOpen(false);
        setClickPosition(null);
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
        if (!clickPosition) return null;

        let left = clickPosition.x;
        let top = clickPosition.y;

        if (left + width > viewportWidth) {
            left = Math.max(0, left - width);
        }

        if (top + height > viewportHeight) {
            top = Math.max(0, top - height);
        }

        return {left, top};
    }, [clickPosition, width, height, viewportWidth, viewportHeight]);

    const renderMenuItems = useCallback((children: () => React.ReactNode) => {
        if (!constrainedPosition) return null;

        return (
            <Menu opened={isOpen} onClose={onClose}>
                <Menu.Dropdown
                    ref={menuRef}
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
    }, [constrainedPosition, isOpen, onClose, menuRef]);

    return {
        onContextMenuTrigger,
        renderMenuItems,
        isOpen,
    };
}
