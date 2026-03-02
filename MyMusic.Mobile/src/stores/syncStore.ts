import {create} from 'zustand';

export type SyncPhase = 'idle' | 'scanning' | 'upload' | 'server' | 'completing' | 'completed' | 'error';

export interface SyncProgress {
    phase: SyncPhase;
    totalFiles: number;
    processedFiles: number;
    currentFile: string;
    created: number;
    updated: number;
    skipped: number;
    downloaded: number;
    removed: number;
    failed: number;
    errorMessage?: string;
    startedAt?: string;
    completedAt?: string;
    eta?: string;
}

interface SyncState {
    progress: SyncProgress;
    isRunning: boolean;
    options: {
        force: boolean;
        dryRun: boolean;
        autoConfirm: boolean;
    };

    startSync: (options: Partial<SyncState['options']>) => void;
    updateProgress: (progress: Partial<SyncProgress>) => void;
    setPhase: (phase: SyncPhase) => void;
    setError: (message: string) => void;
    completeSync: () => void;
    reset: () => void;
    setOptions: (options: Partial<SyncState['options']>) => void;
}

const initialProgress: SyncProgress = {
    phase: 'idle',
    totalFiles: 0,
    processedFiles: 0,
    currentFile: '',
    created: 0,
    updated: 0,
    skipped: 0,
    downloaded: 0,
    removed: 0,
    failed: 0,
};

const initialState = {
    progress: initialProgress,
    isRunning: false,
    options: {
        force: false,
        dryRun: false,
        autoConfirm: false,
    },
};

export const useSyncStore = create<SyncState>()((set) => ({
    ...initialState,

    startSync: (options) => set((state) => ({
        isRunning: true,
        options: {...state.options, ...options},
        progress: {
            ...initialProgress,
            phase: 'scanning',
            startedAt: new Date().toISOString(),
        },
    })),

    updateProgress: (progressUpdate) => set((state) => ({
        progress: {...state.progress, ...progressUpdate},
    })),

    setPhase: (phase) => set((state) => ({
        progress: {...state.progress, phase},
    })),

    setError: (errorMessage) => set((state) => ({
        isRunning: false,
        progress: {
            ...state.progress,
            phase: 'error',
            errorMessage,
        },
    })),

    completeSync: () => set((state) => ({
        isRunning: false,
        progress: {
            ...state.progress,
            phase: 'completed',
            completedAt: new Date().toISOString(),
        },
    })),

    reset: () => set(initialState),

    setOptions: (options) => set((state) => ({
        options: {...state.options, ...options},
    })),
}));