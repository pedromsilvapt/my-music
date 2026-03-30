/* eslint-disable react-refresh/only-export-components */
import React from 'react';
import {create, type StoreApi, type UseBoundStore} from 'zustand';

interface ContextMenuState {
    isOpen: boolean;
    activeMenuId: string | null;
    position: { x: number; y: number } | null;
}

interface ContextMenuActions {
    open: (menuId: string, position: { x: number; y: number }) => void;
    close: () => void;
}

export type ContextMenuStore = UseBoundStore<StoreApi<ContextMenuState & ContextMenuActions>>;

const ContextMenuStoreContext = React.createContext<ContextMenuStore | null>(null);

export function ContextMenuStoreProvider({children}: { children: React.ReactNode }) {
    const storeRef = React.useRef<ContextMenuStore | null>(null);

    if (!storeRef.current) {
        storeRef.current = create<ContextMenuState & ContextMenuActions>((set) => ({
            isOpen: false,
            activeMenuId: null,
            position: null,
            open: (menuId, position) => {
                set({
                    isOpen: true,
                    activeMenuId: menuId,
                    position,
                });
            },
            close: () => {
                set({
                    isOpen: false,
                    activeMenuId: null,
                    position: null,
                });
            },
        }));
    }

    return React.createElement(
        ContextMenuStoreContext.Provider,
        {value: storeRef.current},
        children
    );
}

export function useContextMenuStore<T>(selector: (state: ContextMenuState & ContextMenuActions) => T): T {
    const store = React.useContext(ContextMenuStoreContext);
    if (!store) {
        throw new Error('useContextMenuStore must be used within ContextMenuStoreProvider');
    }
    return store(selector);
}

export function useContextMenuStoreContext(): ContextMenuStore {
    const store = React.useContext(ContextMenuStoreContext);
    if (!store) {
        throw new Error('useContextMenuStoreContext must be used within ContextMenuStoreProvider');
    }
    return store;
}