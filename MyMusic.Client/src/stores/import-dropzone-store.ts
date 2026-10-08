import {create} from 'zustand';

interface ImportDropzoneState {
    /** How many open dialogs take dropped files themselves. */
    suspensions: number;
    suspend: () => void;
    resume: () => void;
}

/**
 * Lets a dialog with a drop area of its own switch off the full screen dropzone that imports dropped files as new
 * songs, which would otherwise show up as soon as a file is dragged over the window.
 */
export const useImportDropzoneStore = create<ImportDropzoneState>((set) => ({
    suspensions: 0,
    suspend: () => set((state) => ({suspensions: state.suspensions + 1})),
    resume: () => set((state) => ({suspensions: Math.max(0, state.suspensions - 1)})),
}));
