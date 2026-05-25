import type { IFileOps } from '../src/services/sync/types';
import * as fs from 'fs';
import * as path from 'path';

export class NodeFileOps implements IFileOps {
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

    async writeFile(filePath: string, data: Blob): Promise<void> {
        const buffer = Buffer.from(await data.arrayBuffer());
        fs.writeFileSync(filePath, buffer);
    }

    async deleteFile(filePath: string): Promise<void> {
        if (fs.existsSync(filePath)) {
            fs.unlinkSync(filePath);
        }
    }

    async readFileBase64(filePath: string): Promise<string> {
        const buffer = fs.readFileSync(filePath);
        return buffer.toString('base64');
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
