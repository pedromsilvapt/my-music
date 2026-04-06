import { getGetArtworkUrl } from "../client/artwork";

export interface ImageDimensions {
    width: number;
    height: number;
}

export interface ArtworkDataUrlResult {
    dataUrl: string;
    dimensions: ImageDimensions;
}

export async function fetchArtworkAsDataUrl(artworkId: number): Promise<ArtworkDataUrlResult> {
    const response = await fetch(getGetArtworkUrl(artworkId));
    if (!response.ok) {
        throw new Error(`Failed to fetch artwork ${artworkId}: ${response.status}`);
    }

    const blob = await response.blob();
    const dataUrl = await blobToDataUrl(blob);
    const dimensions = await loadImageDimensions(dataUrl);

    return { dataUrl, dimensions };
}

function blobToDataUrl(blob: Blob): Promise<string> {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result as string);
        reader.onerror = () => reject(new Error("Failed to read blob as data URL"));
        reader.readAsDataURL(blob);
    });
}

function loadImageDimensions(src: string): Promise<ImageDimensions> {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => resolve({ width: img.width, height: img.height });
        img.onerror = () => reject(new Error("Failed to load image dimensions"));
        img.src = src;
    });
}
