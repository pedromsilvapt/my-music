import {Menu} from "@mantine/core";
import {useViewportSize} from "@mantine/hooks";
import {useEffect, useLayoutEffect, useMemo, useState} from "react";
import {useShallow} from "zustand/react/shallow";
import {useContextMenuStore} from "../../stores/context-menu-store.tsx";
import {constrainToViewport, type Size} from "./context-menu-position.ts";

interface ContextMenuPortalProps {
    menuId: string;
    content: () => React.ReactNode;
}

export function ContextMenuPortal({menuId, content}: ContextMenuPortalProps) {
    // Element kept in state (callback ref) so mounting the dropdown triggers a re-measure,
    // even on the first open, when it mounts after the render that opened it
    const [menuEl, setMenuEl] = useState<HTMLDivElement | null>(null);
    const [size, setSize] = useState<Size>({width: 0, height: 0});
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

    useLayoutEffect(() => {
        if (!menuEl) return;

        // Measure synchronously before paint so the first frame is already constrained
        const measure = () => setSize(prev =>
            prev.width === menuEl.offsetWidth && prev.height === menuEl.offsetHeight
                ? prev
                : {width: menuEl.offsetWidth, height: menuEl.offsetHeight});
        measure();

        const observer = new ResizeObserver(measure);
        observer.observe(menuEl);
        return () => observer.disconnect();
    }, [menuEl, position]);

    const constrainedPosition = useMemo(() => {
        if (!position) return null;

        return constrainToViewport(position, size, {width: viewportWidth, height: viewportHeight});
    }, [position, size, viewportWidth, viewportHeight]);

    if (!isActive || !constrainedPosition) return null;

    return (
        <Menu opened={true} onClose={close}>
            <Menu.Dropdown
                ref={setMenuEl}
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
