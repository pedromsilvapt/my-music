import * as React from 'react';
import {createContext, useContext, useRef} from 'react';
import {create, type StoreApi, useStore} from 'zustand';
import {immer} from 'zustand/middleware/immer';

type QueueManagerState = {
    visibleQueueId: number | null;
    currentQueueId: number | null;
};

type QueueManagerActions = {
    setVisibleQueueId: (id: number | null) => void;
    setCurrentQueueId: (id: number | null) => void;
};

type QueueManagerStore = QueueManagerState & QueueManagerActions;

function createQueueManagerStore(): StoreApi<QueueManagerStore> {
    return create(
        immer<QueueManagerStore>((set) => ({
            visibleQueueId: null,
            currentQueueId: null,
            setVisibleQueueId: (id) =>
                set((state) => {
                    state.visibleQueueId = id;
                }),
            setCurrentQueueId: (id) =>
                set((state) => {
                    state.currentQueueId = id;
                }),
        }))
    );
}

export const QueueManagerContext = createContext<StoreApi<QueueManagerStore>>(null!);

export function QueueManagerProvider({children}: { children: React.ReactNode }) {
    const storeRef = useRef<StoreApi<QueueManagerStore>>(null!);

    if (storeRef.current === null) {
        storeRef.current = createQueueManagerStore();
    }

    return (
        <QueueManagerContext.Provider value={storeRef.current}>
            {children}
        </QueueManagerContext.Provider>
    );
}

export function useQueueManagerStore<U>(selector: (state: QueueManagerStore) => U): U {
    const store = useContext(QueueManagerContext);
    if (!store) {
        throw new Error('Missing QueueManagerProvider');
    }
    return useStore(store, selector);
}

export function useQueueManagerStoreApi() {
    const store = useContext(QueueManagerContext);
    if (!store) {
        throw new Error('Missing QueueManagerProvider');
    }
    return store;
}