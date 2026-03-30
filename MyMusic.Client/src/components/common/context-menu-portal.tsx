import {Menu} from "@mantine/core";
import {useElementSize, useViewportSize} from "@mantine/hooks";
import {useEffect, useMemo} from "react";
import {useShallow} from "zustand/react/shallow";
import {useContextMenuStore} from "../../stores/context-menu-store.tsx";

interface ContextMenuPortalProps {
    menuId: string;
    content: () => React.ReactNode;
}

export function ContextMenuPortal({menuId, content}: ContextMenuPortalProps) {
    const {ref: menuRef, width, height} = useElementSize<HTMLDivElement>();
    const {width: viewportWidth, height: viewportHeight} = useViewportSize();
    
    const {isOpen, activeMenuId, position, close} = useContextMenuStore(
        useShallow(state => ({
            isOpen: state.isOpen,
            activeMenuId: state.activeMenuId,
            position: state.position,
            close: state.close,
        }))
    );

    const isActive = isOpen && activeMenuId === menuId;

    useEffect(() => {
        if (!isActive) return;

        const handleClick = () => {
            close();
        };

        document.addEventListener('click', handleClick);
        return () => document.removeEventListener('click', handleClick);
    }, [isActive, close]);

    const constrainedPosition = useMemo(() => {
        if (!position) return null;

        let left = position.x;
        let top = position.y;

        if (left + width > viewportWidth) {
            left = Math.max(0, left - width);
        }

        if (top + height > viewportHeight) {
            top = Math.max(0, top - height);
        }

        return {left, top};
    }, [position, width, height, viewportWidth, viewportHeight]);

    if (!isActive || !constrainedPosition) return null;

    return (
        <Menu opened={true} onClose={close}>
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
                {content()}
            </Menu.Dropdown>
        </Menu>
    );
}