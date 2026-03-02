import {Directory, File} from 'expo-file-system';
import * as MediaLibrary from 'expo-media-library';

export interface FileMetadata {
    relativePath: string;
    fullPath: string;
    modifiedAt: Date;
    createdAt: Date;
    size: number;
}

export interface ScanOptions {
    extensions: string[];
    excludePatterns: string[];
    basePath: string;
}

export async function scanMusicFiles(options: ScanOptions): Promise<FileMetadata[]> {
    const files: FileMetadata[] = [];

    try {
        const {status} = await MediaLibrary.requestPermissionsAsync();
        if (status !== 'granted') {
            console.log('Media library permission not granted');
            return files;
        }

        const media = await MediaLibrary.getAssetsAsync({
            mediaType: MediaLibrary.MediaType.audio,
            first: 10000,
        });

        for (const asset of media.assets) {
            const uri = asset.uri;
            const filename = asset.filename;
            const ext = '.' + filename.split('.').pop()?.toLowerCase();

            if (!options.extensions.includes(ext)) {
                continue;
            }

            const relativePath = filename;

            if (shouldExclude(relativePath, options.excludePatterns)) {
                continue;
            }

            files.push({
                relativePath,
                fullPath: uri,
                modifiedAt: asset.modificationTime ? new Date(asset.modificationTime * 1000) : new Date(),
                createdAt: asset.creationTime ? new Date(asset.creationTime * 1000) : new Date(),
                size: asset.duration || 0,
            });
        }
    } catch (error) {
        console.error('Error scanning music files:', error);
    }

    return files;
}

export async function scanFromDirectory(directoryUri: string, options: ScanOptions): Promise<FileMetadata[]> {
    const files: FileMetadata[] = [];

    try {
        const directory = new Directory(directoryUri);
        if (!directory.exists) {
            return files;
        }

        const items = listRecursive(directory);

        for (const item of items) {
            if (item instanceof File) {
                const filename = item.name;
                const ext = '.' + filename.split('.').pop()?.toLowerCase();

                if (!options.extensions.includes(ext)) continue;
                if (shouldExclude(item.name, options.excludePatterns)) continue;

                const relativePath = item.uri.replace(directoryUri + '/', '');

                files.push({
                    relativePath,
                    fullPath: item.uri,
                    modifiedAt: item.modificationTime ? new Date(item.modificationTime) : new Date(),
                    createdAt: item.creationTime ? new Date(item.creationTime) : new Date(),
                    size: item.size || 0,
                });
            }
        }
    } catch (error) {
        console.error('Error scanning directory:', error);
    }

    return files;
}

function listRecursive(dir: Directory): (Directory | File)[] {
    const results: (Directory | File)[] = [];
    const items = dir.list();

    for (const item of items) {
        results.push(item);
        if (item instanceof Directory) {
            results.push(...listRecursive(item));
        }
    }

    return results;
}

function shouldExclude(filename: string, patterns: string[]): boolean {
    for (const pattern of patterns) {
        const regex = globToRegex(pattern);
        if (regex.test(filename)) return true;
        if (regex.test('/' + filename)) return true;
    }
    return false;
}

function globToRegex(glob: string): RegExp {
    let regex = '^';
    for (const c of glob) {
        regex += c === '*'
            ? '.*'
            : c === '?'
                ? '.'
                : c === '.'
                    ? '\\.'
                    : c === '/'
                        ? '[\\\\/]'
                        : c === '\\'
                            ? '[\\\\/]'
                            : c === '['
                                ? '['
                                : c === ']'
                                    ? ']'
                                    : escapeRegExp(c);
    }
    regex += '$';
    return new RegExp(regex, 'i');
}

function escapeRegExp(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}