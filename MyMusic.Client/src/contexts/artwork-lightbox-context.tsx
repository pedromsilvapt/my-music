/* eslint-disable react-refresh/only-export-components */
import {createContext, useContext, useState} from "react";
import ArtworkLightbox from "../components/common/artwork-lightbox.tsx";

interface ArtworkLightboxContextValue {
    openLightbox: (src: string) => void;
    closeLightbox: () => void;
    isOpen: boolean;
    currentSrc: string | null;
}

export const ArtworkLightboxContext = createContext<ArtworkLightboxContextValue>(null!);

export function useArtworkLightbox() {
    const context = useContext(ArtworkLightboxContext);
    if (!context) {
        throw new Error('Missing ArtworkLightboxProvider');
    }
    return context;
}

interface ArtworkLightboxProviderProps {
    children: React.ReactNode;
}

export function ArtworkLightboxProvider({children}: ArtworkLightboxProviderProps) {
    const [opened, setOpened] = useState(false);
    const [src, setSrc] = useState<string | null>(null);

    const openLightbox = (newSrc: string) => {
        setSrc(newSrc);
        setOpened(true);
    };

    const closeLightbox = () => {
        setOpened(false);
        setSrc(null);
    };

    return (
        <ArtworkLightboxContext.Provider value={{openLightbox, closeLightbox, isOpen: opened, currentSrc: src}}>
            {children}
            {src && (
                <ArtworkLightbox
                    opened={opened}
                    onClose={closeLightbox}
                    src={src}
                />
            )}
        </ArtworkLightboxContext.Provider>
    );
}