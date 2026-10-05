import {getMusicExtensions} from './configService';
import {getScanner, type ScannerType} from './scannerRegistry';

/**
 * Lists the music files of the repository, as paths relative to it, with no exclusion rule applied:
 * the list the rules of the device settings screen are tried against.
 */
export async function scanRepositoryPaths(repositoryPath: string, scannerType: ScannerType): Promise<string[]> {
    const result = await getScanner(scannerType)(repositoryPath, {
        extensions: getMusicExtensions(),
        excludePatterns: [],
        basePath: repositoryPath,
    });

    // A scan that found nothing because it failed must not read as "no file is excluded"
    if (result.files.length === 0 && result.errors.length > 0) {
        throw new Error(result.errors[0].error);
    }

    return result.files.map(file => file.relativePath);
}
