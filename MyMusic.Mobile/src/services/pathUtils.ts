export function isContentUri(path: string): boolean {
    return path.startsWith('content://');
}

export function normalizePath(path: string): string {
    if (isContentUri(path)) {
        return path;
    }
    if (path.startsWith('file://')) {
        path = path.substring(7);
    }
    if (path.endsWith('/')) {
        path = path.slice(0, -1);
    }
    return path;
}

export function isWithinDirectory(filePath: string, directoryPath: string): boolean {
    if (!directoryPath) {
        return false;
    }
    const normalizedDir = directoryPath.endsWith('/')
        ? directoryPath
        : directoryPath + '/';
    return filePath.startsWith(normalizedDir) || filePath === directoryPath;
}

export function computeRelativePath(filePath: string, repoFsPath: string, fallbackFilename: string): string {
    if (repoFsPath && isWithinDirectory(filePath, repoFsPath)) {
        let rel = filePath.substring(repoFsPath.length);
        if (rel.startsWith('/')) rel = rel.slice(1);
        return rel;
    }
    return fallbackFilename;
}

/**
 * Converts a filesystem path to a file:// URI with proper encoding.
 * If the path is already a URI (starts with protocol://), returns it as-is.
 * This is required for expo-file-system File class which expects URIs.
 */
export function toFileUri(path: string): string {
    // If already a URI (has protocol), return as-is
    if (/^[a-z][a-z0-9+.-]*:\/\//i.test(path)) {
        return path;
    }
    // Convert filesystem path to file:// URI with encoded special characters
    // encodeURIComponent then restore forward slashes
    return 'file://' + encodeURIComponent(path).replace(/%2F/g, '/');
}
