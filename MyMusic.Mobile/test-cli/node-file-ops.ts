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
