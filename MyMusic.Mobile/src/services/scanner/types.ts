export interface FileMetadata {
    relativePath: string;
    fullPath: string;
    modifiedAt: Date;
    createdAt: Date;
    size: number;
}

export interface ScanError {
    path: string;
    error: string;
}

export interface ScanOptions {
    extensions: string[];
    excludePatterns: string[];
    basePath: string;
    onProgress?: (scannedCount: number, currentDir: string) => void;
    onError?: (path: string, error: string) => void;
}

export interface ScanResult {
    files: FileMetadata[];
    errors: ScanError[];
}

export type ScannerFunction = (
    directoryUri: string,
    options: ScanOptions
) => Promise<ScanResult>;
