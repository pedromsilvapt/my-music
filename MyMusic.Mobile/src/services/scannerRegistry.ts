import {
    scanFromDirectory as scanFromDirectoryFileSystem,
} from './fileScanner';
import {
    scanFromDirectory as scanFromDirectoryMediaLibrary,
} from './mediaLibraryScanner';
import {type ScannerFunction, type ScanOptions, type ScanResult} from './scanner/types';

export type ScannerType = 'fileSystem' | 'mediaLibrary';

export type {ScannerFunction, ScanOptions, ScanResult} from './scanner/types';

const scannerRegistry: Record<ScannerType, ScannerFunction> = {
    fileSystem: scanFromDirectoryFileSystem,
    mediaLibrary: scanFromDirectoryMediaLibrary,
};

export function getScanner(type: ScannerType): ScannerFunction {
    const scanner = scannerRegistry[type];
    if (!scanner) {
        throw new Error(`Unknown scanner type: ${type}`);
    }
    return scanner;
}

export function getScannerOptions(): {value: ScannerType; label: string}[] {
    return [
        {value: 'fileSystem', label: 'File System'},
        {value: 'mediaLibrary', label: 'Media Library'},
    ];
}
