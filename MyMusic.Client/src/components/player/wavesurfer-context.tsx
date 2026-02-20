import {createContext, type RefObject, useContext, useRef} from 'react';
import type WaveSurfer from 'wavesurfer.js';

const WavesurferContext = createContext<RefObject<WaveSurfer | null> | null>(null);

export function WavesurferProvider({children}: { children: React.ReactNode }) {
    const wavesurferRef = useRef<WaveSurfer | null>(null);
    return (
        <WavesurferContext.Provider value={wavesurferRef}>
            {children}
        </WavesurferContext.Provider>
    );
}

export function useWavesurferRef(): RefObject<WaveSurfer | null> {
    const ref = useContext(WavesurferContext);
    if (!ref) {
        throw new Error('useWavesurferRef must be used within a WavesurferProvider');
    }
    return ref;
}
