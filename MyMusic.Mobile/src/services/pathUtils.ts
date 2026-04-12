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

export function decodeSafUriToFilesystemPath(uri: string): string {
    try {
        if (!isContentUri(uri)) {
            return normalizePath(uri);
        }

        const parsed = new URL(uri);
        const pathSegments = parsed.pathname.split('/').filter(Boolean);

        const docIndex = pathSegments.indexOf('document');
        const segIndex = docIndex !== -1 ? docIndex : pathSegments.indexOf('tree');
        if (segIndex === -1 || segIndex + 1 >= pathSegments.length) {
            return '';
        }

        const docPath = decodeURIComponent(pathSegments[segIndex + 1]);

        if (docPath.startsWith('primary:')) {
            const relativePath = docPath.substring('primary:'.length);
            return relativePath.length > 0
                ? `/storage/emulated/0/${relativePath}`
                : '/storage/emulated/0';
        }

        if (docPath.startsWith('home:')) {
            const relativePath = docPath.substring('home:'.length);
            return relativePath.length > 0
                ? `/storage/emulated/0/${relativePath}`
                : '/storage/emulated/0';
        }

        const colonIndex = docPath.indexOf(':');
        if (colonIndex !== -1) {
            const volume = docPath.substring(0, colonIndex);
            const relativePath = docPath.substring(colonIndex + 1);
            if (relativePath.length > 0) {
                return `/storage/${volume}/${relativePath}`;
            }
            return `/storage/${volume}`;
        }

        return '';
    } catch {
        return '';
    }
}

export function decodeToFsPath(uri: string): string {
    if (isContentUri(uri)) {
        const decoded = decodeSafUriToFilesystemPath(uri);
        return decoded || normalizePath(uri);
    }
    return normalizePath(uri);
}

export function computeRelativePath(filePath: string, repoFsPath: string, fallbackFilename: string): string {
    if (repoFsPath && isWithinDirectory(filePath, repoFsPath)) {
        let rel = filePath.substring(repoFsPath.length);
        if (rel.startsWith('/')) rel = rel.slice(1);
        return rel;
    }
    return fallbackFilename;
}
