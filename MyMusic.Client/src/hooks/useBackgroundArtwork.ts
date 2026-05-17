import {useEffect, useState} from "react";
import {convertArtworkUrlToBase64} from "../utils/artwork";

export interface BackgroundArtworkState {
    base64: string | null;
    dimensions: { width: number; height: number } | null;
    isLoading: boolean;
    error: string | null;
}

export function useBackgroundArtwork(url: string | undefined): BackgroundArtworkState {
    const [state, setState] = useState<BackgroundArtworkState>({
        base64: null,
        dimensions: null,
        isLoading: false,
        error: null,
    });

    useEffect(() => {
        if (!url) {
            setState({ base64: null, dimensions: null, isLoading: false, error: null });
            return;
        }

        let cancelled = false;

        setState({ base64: null, dimensions: null, isLoading: true, error: null });

        convertArtworkUrlToBase64(url)
            .then((base64) => {
                if (cancelled) return;
                
                const img = new Image();
                img.onload = () => {
                    if (cancelled) return;
                    setState({
                        base64,
                        dimensions: { width: img.width, height: img.height },
                        isLoading: false,
                        error: null,
                    });
                };
                img.onerror = () => {
                    if (cancelled) return;
                    setState({
                        base64: null,
                        dimensions: null,
                        isLoading: false,
                        error: "Failed to load image dimensions",
                    });
                };
                img.src = base64;
            })
            .catch((err) => {
                if (cancelled) return;
                setState({
                    base64: null,
                    dimensions: null,
                    isLoading: false,
                    error: err instanceof Error ? err.message : "Failed to download artwork",
                });
            });

        return () => {
            cancelled = true;
        };
    }, [url]);

    return state;
}