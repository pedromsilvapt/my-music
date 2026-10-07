import {create} from 'zustand';
import type {ScannerType} from '../services/scannerRegistry';
import type {ConflictResolution, PromptAnswer, SyncDirection} from '../services/sync/types';

export type SyncPhase = 'idle' | 'scanning' | 'fingerprinting' | 'upload' | 'resolving' | 'server' | 'committing' | 'completing' | 'completed' | 'error';

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
    deleteLocal: number;
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

/** A question the running sync waits on, shown as a dialog over the progress screen. */
export type SyncPrompt =
    | {kind: 'deletion'; filePath: string; answer: (answer: PromptAnswer<boolean>) => void}
    | {kind: 'conflict'; filePath: string; choices: ConflictResolution[]; answer: (answer: PromptAnswer<ConflictResolution>) => void};

/** Answers a question nobody will answer any more, changing nothing: the file is kept, the conflict skipped. */
function declinePrompt(prompt: SyncPrompt | null) {
    if (prompt?.kind === 'deletion') {
        prompt.answer({value: false, applyToAll: false});
    } else if (prompt?.kind === 'conflict') {
        prompt.answer({value: 'skip', applyToAll: false});
    }
}

interface SyncState {
    progress: SyncProgress;
    pendingPrompt: SyncPrompt | null;
    isRunning: boolean;
    isCancelled: boolean;
    options: {
        force: boolean;
        dryRun: boolean;
        autoConfirm: boolean;
        treatConflictsAsErrors: boolean;
        scannerType: ScannerType;
        direction: SyncDirection;
        deduplicate: boolean;
        recordSkipped: boolean;
    };

    startSync: (options: Partial<SyncState['options']>) => void;
    updateProgress: (progress: Partial<SyncProgress>) => void;
    setPhase: (phase: SyncPhase) => void;
    setError: (message: string) => void;
    completeSync: () => void;
    cancelSync: () => void;
    reset: () => void;
    setOptions: (options: Partial<SyncState['options']>) => void;
    showPrompt: (prompt: SyncPrompt) => void;
    closePrompt: () => void;
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
    deleteLocal: 0,
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
    pendingPrompt: null,
    isRunning: false,
    isCancelled: false,
    options: {
        force: false,
        dryRun: false,
        autoConfirm: false,
        treatConflictsAsErrors: false,
        scannerType: 'fileSystem' as ScannerType,
        direction: 'Both' as SyncDirection,
        deduplicate: false,
        recordSkipped: false,
    },
};

export const useSyncStore = create<SyncState>()((set, get) => ({
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

    cancelSync: () => {
        // The sync only stops once the question it waits on is answered
        declinePrompt(get().pendingPrompt);
        set((state) => ({
            isRunning: false,
            isCancelled: true,
            pendingPrompt: null,
            progress: {
                ...state.progress,
                phase: 'idle',
                isCancelled: true,
            },
        }));
    },

    reset: () => {
        declinePrompt(get().pendingPrompt);
        set(initialState);
    },

    setOptions: (options) => set((state) => ({
        options: {...state.options, ...options},
    })),

    showPrompt: (prompt) => set({pendingPrompt: prompt}),

    closePrompt: () => set({pendingPrompt: null}),
}));