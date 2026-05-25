import {create} from 'zustand';
import type {ScannerType} from '../services/scannerRegistry';

export type SyncPhase = 'idle' | 'scanning' | 'upload' | 'resolving' | 'server' | 'committing' | 'completing' | 'completed' | 'error';

export interface SyncProgress {
    phase: SyncPhase;
    totalFiles: number;
    estimatedTotalFiles: number;
    processedFiles: number;
    scannedFiles: number;
    currentFile: string;
    createRemote: number;
    updateRemote: number;
    createLocal: number;
    updateLocal: number;
    delete: number;
    link: number;
    unlink: number;
    rename: number;
    skipped: number;
    conflict: number;
    updateTimestamp: number;
    error: number;
    errorMessage?: string;
    startedAt?: string;
    completedAt?: string;
    eta?: string;
    isCancelled?: boolean;
}

interface SyncState {
    progress: SyncProgress;
    isRunning: boolean;
    isCancelled: boolean;
    options: {
        force: boolean;
        dryRun: boolean;
        autoConfirm: boolean;
        treatConflictsAsErrors: boolean;
        scannerType: ScannerType;
    };

    startSync: (options: Partial<SyncState['options']>) => void;
    updateProgress: (progress: Partial<SyncProgress>) => void;
    setPhase: (phase: SyncPhase) => void;
    setError: (message: string) => void;
    completeSync: () => void;
    cancelSync: () => void;
    reset: () => void;
    setOptions: (options: Partial<SyncState['options']>) => void;
}

const initialProgress: SyncProgress = {
    phase: 'idle',
    totalFiles: 0,
    estimatedTotalFiles: 0,
    processedFiles: 0,
    scannedFiles: 0,
    currentFile: '',
    createRemote: 0,
    updateRemote: 0,
    createLocal: 0,
    updateLocal: 0,
    delete: 0,
    link: 0,
    unlink: 0,
    rename: 0,
    skipped: 0,
    conflict: 0,
    updateTimestamp: 0,
    error: 0,
    isCancelled: false,
};

const initialState = {
    progress: initialProgress,
    isRunning: false,
    isCancelled: false,
    options: {
        force: false,
        dryRun: false,
        autoConfirm: false,
        treatConflictsAsErrors: false,
        scannerType: 'fileSystem' as ScannerType,
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

    cancelSync: () => set((state) => ({
        isRunning: false,
        isCancelled: true,
        progress: {
            ...state.progress,
            phase: 'idle',
            isCancelled: true,
        },
    })),

    reset: () => set(initialState),

    setOptions: (options) => set((state) => ({
        options: {...state.options, ...options},
    })),
}));