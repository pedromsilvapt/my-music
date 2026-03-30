import {useCallback} from "react";
import {useContextMenuStoreContext} from "../stores/context-menu-store.tsx";

export function useContextMenuTrigger(menuId: string) {
    const store = useContextMenuStoreContext();

    const trigger = useCallback((event: React.MouseEvent | React.TouchEvent) => {
        event.preventDefault();

        let clientX: number, clientY: number;

        if ('touches' in event) {
            clientX = event.touches[0].clientX;
            clientY = event.touches[0].clientY;
        } else {
            clientX = event.clientX;
            clientY = event.clientY;
        }

        store.getState().open(menuId, {x: clientX, y: clientY});
    }, [menuId, store]);

    return {trigger};
}