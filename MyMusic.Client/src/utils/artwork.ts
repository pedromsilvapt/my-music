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

export async function convertArtworkUrlToBase64(
    urlOrBase64: string
): Promise<string> {
    if (urlOrBase64.startsWith('data:')) {
        return urlOrBase64;
    }

    if (urlOrBase64.includes('/api/artwork/')) {
        const match = urlOrBase64.match(/\/api\/artwork\/(\d+)/);
        if (match) {
            const artworkId = parseInt(match[1], 10);
            const { dataUrl } = await fetchArtworkAsDataUrl(artworkId);
            return dataUrl;
        }
    }

    if (urlOrBase64.includes('/api/thumbnail-proxy/')) {
        const match = urlOrBase64.match(/\/api\/thumbnail-proxy\/(.+)$/);
        if (match) {
            const encoded = match[1];
            const originalUrl = decodeBase64Url(encoded);
            return fetchUrlAndConvertToBase64(originalUrl);
        }
    }

    return fetchUrlAndConvertToBase64(urlOrBase64);
}

function decodeBase64Url(encoded: string): string {
    let base64 = encoded
        .replace(/-/g, '+')
        .replace(/_/g, '/');

    const padding = base64.length % 4;
    if (padding === 2) {
        base64 += '==';
    } else if (padding === 3) {
        base64 += '=';
    }

    const binaryString = atob(base64);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return new TextDecoder('utf-8').decode(bytes);
}

async function fetchUrlAndConvertToBase64(url: string): Promise<string> {
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Failed to fetch image from ${url}: ${response.status}`);
    }
    const blob = await response.blob();
    return blobToDataUrl(blob);
}
