import * as React from 'react';
import {createContext, useContext, useRef} from 'react';
import {create, type StoreApi, useStore} from 'zustand';
import {immer} from 'zustand/middleware/immer';
import {useShallow} from 'zustand/react/shallow';
import type {CollectionView} from '../components/common/collection/collection-toolbar.tsx';
import type {CollectionSort} from '../components/common/collection/collection.tsx';

export interface ScrollPosition {
    index: number;
    offset: number;
}

export interface CollectionState<T = unknown> {
    view: CollectionView;
    sort: CollectionSort<T>;
    clientSearch: string;
    clientFilter: string;
    serverSearch: string;
    serverFilter: string;
    scrollPosition: ScrollPosition | null;
}

type CollectionStoreState = {
    collections: Record<string, CollectionState>;
};

type CollectionStoreActions = {
    getCollectionState: (key: string) => CollectionState;
    setCollectionView: (key: string, view: CollectionView) => void;
    setCollectionSort: (key: string, sort: CollectionSort<unknown>) => void;
    setCollectionClientSearch: (key: string, search: string) => void;
    setCollectionClientFilter: (key: string, filter: string) => void;
    setCollectionServerSearch: (key: string, search: string) => void;
    setCollectionServerFilter: (key: string, filter: string) => void;
    setCollectionScrollPosition: (key: string, position: ScrollPosition | null) => void;
    clearCollectionState: (key: string) => void;
};

type CollectionStore = CollectionStoreState & CollectionStoreActions;

const DEFAULT_COLLECTION_STATE: CollectionState = {
    view: 'table',
    sort: [],
    clientSearch: '',
    clientFilter: '',
    serverSearch: '',
    serverFilter: '',
    scrollPosition: null,
};

function createCollectionStore(): StoreApi<CollectionStore> {
    return create(
        immer<CollectionStore>((set, get) => ({
            collections: {},
            getCollectionState: (key: string): CollectionState => {
                const state = get().collections[key];
                return state ?? DEFAULT_COLLECTION_STATE;
            },
            setCollectionView: (key: string, view: CollectionView) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].view = view;
                });
            },
            setCollectionSort: (key: string, sort: CollectionSort<unknown>) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].sort = sort;
                });
            },
            setCollectionClientSearch: (key: string, search: string) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].clientSearch = search;
                });
            },
            setCollectionClientFilter: (key: string, filter: string) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].clientFilter = filter;
                });
            },
            setCollectionServerSearch: (key: string, search: string) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].serverSearch = search;
                });
            },
            setCollectionServerFilter: (key: string, filter: string) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].serverFilter = filter;
                });
            },
            setCollectionScrollPosition: (key: string, position: ScrollPosition | null) => {
                set((state) => {
                    if (!state.collections[key]) {
                        state.collections[key] = {...DEFAULT_COLLECTION_STATE};
                    }
                    state.collections[key].scrollPosition = position;
                });
            },
            clearCollectionState: (key: string) => {
                set((state) => {
                    delete state.collections[key];
                });
            },
        }))
    );
}

export const CollectionStoreContext = createContext<StoreApi<CollectionStore>>(null!);

export function CollectionStoreProvider({children}: { children: React.ReactNode }) {
    const storeRef = useRef<StoreApi<CollectionStore>>(null!);

    if (storeRef.current === null) {
        storeRef.current = createCollectionStore();
    }

    return (
        <CollectionStoreContext.Provider value={storeRef.current}>
            {children}
        </CollectionStoreContext.Provider>
    );
}

export function useCollectionStore<U>(selector: (state: CollectionStore) => U): U {
    const store = useContext(CollectionStoreContext);
    if (!store) {
        throw new Error('Missing CollectionStoreProvider');
    }
    return useStore(store, selector);
}

export const useCollectionActions = <S extends object>(selector: (state: CollectionStore) => S): S => {
    return useCollectionStore(useShallow(selector));
};

export function useCollectionStoreApi() {
    const store = useContext(CollectionStoreContext);
    if (!store) {
        throw new Error('Missing CollectionStoreProvider');
    }
    return store;
}

export function useCollectionStateByKey(key: string): CollectionState {
    return useCollectionStore((state) => state.getCollectionState(key));
}