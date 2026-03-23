import React from 'react';
import {create, type StoreApi, type UseBoundStore} from 'zustand';

interface SelectionState {
    selectedKeys: Set<React.Key>;
    hasSelection: boolean;
    lastSelectedKey: React.Key | null;
    lastSelectedElement: HTMLElement | null;
}

interface SelectionActions {
    setSelection: (keys: React.Key[]) => void;
    toggle: (key: React.Key) => void;
    reset: () => void;
    setLastSelectedKey: (key: React.Key | null) => void;
    setLastSelectedElement: (element: HTMLElement | null) => void;
}

export type SelectionStore = UseBoundStore<StoreApi<SelectionState & SelectionActions>>;

const SelectionStoreContext = React.createContext<SelectionStore | null>(null);

export function SelectionStoreProvider({store, children}: { store: SelectionStore; children: React.ReactNode }) {
    return React.createElement(SelectionStoreContext.Provider, {value: store}, children);
}

export function useSelectionStoreContext(): SelectionStore {
    const store = React.useContext(SelectionStoreContext);
    if (!store) {
        throw new Error('useSelectionStoreContext must be used within SelectionStoreProvider');
    }
    return store;
}

export const createSelectionStore = (): SelectionStore => {
    return create<SelectionState & SelectionActions>((set) => ({
        selectedKeys: new Set(),
        hasSelection: false,
        lastSelectedKey: null,
        lastSelectedElement: null,
        setSelection: (keys) => {
            set({
                selectedKeys: new Set(keys),
                hasSelection: keys.length > 0
            });
        },
        toggle: (key) => {
            set(state => {
                const currentKeys = state.selectedKeys;
                const newKeys = new Set(currentKeys);
                if (newKeys.has(key)) {
                    newKeys.delete(key);
                } else {
                    newKeys.add(key);
                }
                return {
                    selectedKeys: newKeys,
                    hasSelection: newKeys.size > 0
                };
            });
        },
        reset: () => {
            set({
                selectedKeys: new Set(),
                hasSelection: false
            });
        },
        setLastSelectedKey: (key) => {
            set({lastSelectedKey: key});
        },
        setLastSelectedElement: (element) => {
            set({lastSelectedElement: element});
        },
    }));
};

export function useSelectionCount(store: SelectionStore | null): number {
    return store ? store(state => state.selectedKeys.size) : 0;
}

export function useHasSelection(store: SelectionStore): boolean {
    return store(state => state.hasSelection);
}