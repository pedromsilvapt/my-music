import type { IFileSystemScanner, ScannerOptions, ScannerResult, SyncFileInfo, ScanError } from '../src/services/sync/types';
import * as fs from 'fs';
import * as path from 'path';
import { createExclusionMatcher } from '../src/services/sync/exclusions';

export const nodeScanner: IFileSystemScanner = async (
    directoryUri: string,
    options: ScannerOptions
): Promise<ScannerResult> => {
    const files: SyncFileInfo[] = [];
    const errors: ScanError[] = [];
    const extensions = options.extensions.map(e => e.toLowerCase());
    const isExcluded = createExclusionMatcher(options.excludePatterns);

    const basePath = options.basePath.replace(/\\/g, '/').replace(/\/$/, '');

    function scanDir(currentDir: string): void {
        try {
            const entries = fs.readdirSync(currentDir, { withFileTypes: true });
            for (const entry of entries) {
                const fullPath = path.join(currentDir, entry.name);
                const normalizedFullPath = fullPath.replace(/\\/g, '/');
                const relativePath = normalizedFullPath.startsWith(basePath + '/')
                    ? normalizedFullPath.substring(basePath.length + 1)
                    : normalizedFullPath;

                if (isExcluded(entry.isDirectory() ? `${relativePath}/` : relativePath)) {
                    continue;
                }

                if (entry.isDirectory()) {
                    scanDir(fullPath);
                } else if (entry.isFile()) {
                    const ext = path.extname(entry.name).toLowerCase();
                    if (extensions.length > 0 && !extensions.includes(ext)) {
                        continue;
                    }

                    try {
                        const stats = fs.statSync(fullPath);
                        files.push({
                            relativePath,
                            fullPath,
                            modifiedAt: stats.mtime,
                            createdAt: stats.birthtime || stats.mtime,
                            size: stats.size,
                        });
                    } catch (e) {
                        errors.push({
                            path: relativePath,
                            error: e instanceof Error ? e.message : String(e),
                        });
                    }

                    options.onProgress?.(files.length, currentDir);
                }
            }
        } catch (e) {
            const normalizedDir = currentDir.replace(/\\/g, '/');
            const relPath = normalizedDir.startsWith(basePath + '/')
                ? normalizedDir.substring(basePath.length + 1)
                : normalizedDir;
            errors.push({
                path: relPath,
                error: e instanceof Error ? e.message : String(e),
            });
        }
    }

    const decodedPath = directoryUri.startsWith('file://')
        ? directoryUri.substring(7)
        : directoryUri;

    scanDir(decodedPath);

    return { files, errors };
};
