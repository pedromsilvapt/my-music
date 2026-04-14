export function safeToIsoString(date: Date | undefined): string | undefined {
    if (!date) {
        return undefined;
    }
    return isNaN(date.getTime()) ? new Date().toISOString() : date.toISOString();
}

export function chunkArray<T>(array: T[], size: number): T[][] {
    const chunks: T[][] = [];
    for (let i = 0; i < array.length; i += size) {
        chunks.push(array.slice(i, i + size));
    }
    return chunks;
}

export function formatFilePath(path: string, repositoryPath: string): string {
    let formatted = path;

    if (repositoryPath && formatted.startsWith(repositoryPath)) {
        formatted = formatted.slice(repositoryPath.length);
    }

    if (formatted.startsWith('/')) {
        formatted = formatted.slice(1);
    }

    try {
        formatted = decodeURIComponent(formatted);
    } catch {
        // Ignore decoding errors
    }

    return formatted;
}
