import {
    scanFromDirectory as scanFromDirectoryFileSystem,
    type ScanOptions,
    type ScanResult,
} from './fileScanner';
import {
    scanFromDirectory as scanFromDirectoryMediaLibrary,
} from './mediaLibraryScanner';

/**
 * Available scanner implementations
 */
export type ScannerType = 'fileSystem' | 'mediaLibrary';

/**
 * Scanner interface - both implementations must conform to this
 */
export type ScannerFunction = (
    directoryUri: string,
    options: ScanOptions
) => Promise<ScanResult>;

/**
 * Registry mapping scanner types to their implementations
 */
const scannerRegistry: Record<ScannerType, ScannerFunction> = {
    fileSystem: scanFromDirectoryFileSystem,
    mediaLibrary: scanFromDirectoryMediaLibrary,
};

/**
 * Get the scanner implementation for a given type
 * @param type The scanner type to use
 * @returns The scanner function
 */
export function getScanner(type: ScannerType): ScannerFunction {
    const scanner = scannerRegistry[type];
    if (!scanner) {
        throw new Error(`Unknown scanner type: ${type}`);
    }
    return scanner;
}

/**
 * Get all available scanner types with their display names
 */
export function getScannerOptions(): {value: ScannerType; label: string}[] {
    return [
        {value: 'fileSystem', label: 'File System'},
        {value: 'mediaLibrary', label: 'Media Library'},
    ];
}
