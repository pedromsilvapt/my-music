import type { IFileOps } from '../src/services/sync/types';
import * as fs from 'fs';
import * as path from 'path';
import { createXXHash128 } from 'hash-wasm';

export class NodeFileOps implements IFileOps {
    async resolveRepositoryPath(repositoryPath: string): Promise<string> {
        return repositoryPath.startsWith('file://') ? repositoryPath.substring(7) : repositoryPath;
    }

    fileExists(filePath: string): boolean {
        return fs.existsSync(filePath);
    }

    directoryExists(dirPath: string): boolean {
        try {
            return fs.statSync(dirPath).isDirectory();
        } catch {
            return false;
        }
    }

    async ensureDirectory(filePath: string): Promise<void> {
        const dir = path.dirname(filePath);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
    }

    async deleteFile(filePath: string): Promise<void> {
        if (fs.existsSync(filePath)) {
            fs.unlinkSync(filePath);
        }
    }

    async computeChecksum(filePath: string, algorithm: string): Promise<string> {
        if (algorithm !== 'XxHash128') {
            throw new Error(`Unsupported checksum algorithm: ${algorithm}`);
        }

        // The digest is the canonical (big-endian) form of the hash, like the server's
        const hasher = await createXXHash128();
        for await (const chunk of fs.createReadStream(filePath)) {
            hasher.update(chunk as Buffer);
        }
        return Buffer.from(hasher.digest('binary')).toString('base64');
    }

    getModificationTime(filePath: string): Date | null {
        try {
            const stats = fs.statSync(filePath);
            return stats.mtime;
        } catch {
            return null;
        }
    }

    async setModificationTime(filePath: string, modifiedAt: Date): Promise<void> {
        fs.utimesSync(filePath, fs.statSync(filePath).atime, modifiedAt);
    }

    async listModificationTimes(rootPath: string, extensions: string[]): Promise<Map<string, Date>> {
        const wanted = new Set(extensions.map(extension => extension.toLowerCase()));
        const times = new Map<string, Date>();

        const walk = (directory: string, relativeDirectory: string) => {
            let entries: fs.Dirent[];
            try {
                entries = fs.readdirSync(directory, { withFileTypes: true });
            } catch {
                return;
            }

            for (const entry of entries) {
                const relativePath = relativeDirectory ? `${relativeDirectory}/${entry.name}` : entry.name;
                const fullPath = path.join(directory, entry.name);
                if (entry.isDirectory()) {
                    walk(fullPath, relativePath);
                } else if (entry.isFile() && wanted.has(path.extname(entry.name).toLowerCase())) {
                    const mtime = this.getModificationTime(fullPath);
                    if (mtime) {
                        times.set(relativePath, mtime);
                    }
                }
            }
        };

        walk(rootPath, '');
        return times;
    }

    async setModificationTimes(files: Array<{ path: string; modifiedAt: Date }>): Promise<Array<string | null>> {
        return Promise.all(files.map(async file => {
            try {
                await this.setModificationTime(file.path, file.modifiedAt);
                return null;
            } catch (error) {
                return error instanceof Error ? error.message : String(error);
            }
        }));
    }

    async moveFile(fromPath: string, toPath: string): Promise<void> {
        const dir = path.dirname(toPath);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        fs.renameSync(fromPath, toPath);
    }

    async copyFile(fromPath: string, toPath: string): Promise<void> {
        const dir = path.dirname(toPath);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        fs.copyFileSync(fromPath, toPath);
    }

    async deleteEmptyDirectories(filePath: string, basePath: string): Promise<void> {
        let currentDir = path.dirname(filePath);
        while (currentDir.length > basePath.length && fs.existsSync(currentDir)) {
            const files = fs.readdirSync(currentDir);
            if (files.length === 0) {
                fs.rmdirSync(currentDir);
                currentDir = path.dirname(currentDir);
            } else {
                break;
            }
        }
    }
}
