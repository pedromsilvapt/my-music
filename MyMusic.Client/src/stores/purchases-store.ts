import {create} from 'zustand';

interface PurchasesState {
    pendingAutoDownloads: Set<number>;
    addPendingAutoDownload: (id: number) => void;
    removePendingAutoDownload: (id: number) => void;
    clearPendingAutoDownloads: () => void;
}

export const usePurchasesStore = create<PurchasesState>((set) => ({
    pendingAutoDownloads: new Set<number>(),
    addPendingAutoDownload: (id) =>
        set((state) => {
            const next = new Set(state.pendingAutoDownloads);
            next.add(id);
            return {pendingAutoDownloads: next};
        }),
    removePendingAutoDownload: (id) =>
        set((state) => {
            const next = new Set(state.pendingAutoDownloads);
            next.delete(id);
            return {pendingAutoDownloads: next};
        }),
    clearPendingAutoDownloads: () =>
        set({pendingAutoDownloads: new Set<number>()}),
}));