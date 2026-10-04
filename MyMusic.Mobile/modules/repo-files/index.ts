import { requireNativeModule } from 'expo';

/**
 * File operations on the music repository in shared storage, done natively with plain filesystem
 * paths (or `file://` URIs). expo-file-system rejects any operation whose target does not exist yet
 * outside the app sandbox, so it cannot create, replace or rename the repository files.
 *
 * Android only. Writing needs the "All files access" permission, see {@link hasAllFilesAccess}.
 */

interface RepoFilesNativeModule {
    hasAllFilesAccess(): Promise<boolean>;
    requestAllFilesAccess(): Promise<void>;
    downloadFile(url: string, headers: Record<string, string>, destinationPath: string): Promise<void>;
    ensureDirectory(path: string): Promise<void>;
    moveFile(fromPath: string, toPath: string): Promise<void>;
    copyFile(fromPath: string, toPath: string): Promise<void>;
    deleteFile(path: string): Promise<void>;
}

// Resolved on use, so a build without the module only fails when the repository is actually written to
const native = () => requireNativeModule<RepoFilesNativeModule>('RepoFiles');

/** Whether the app holds "All files access", which writing to the repository needs. */
export function hasAllFilesAccess(): Promise<boolean> {
    return native().hasAllFilesAccess();
}

/** Opens the system screen where the user grants "All files access" to the app. */
export function requestAllFilesAccess(): Promise<void> {
    return native().requestAllFilesAccess();
}

/** Streams a URL to a file, replacing it. A failed download leaves no partial file. */
export function downloadFile(url: string, headers: Record<string, string>, destinationPath: string): Promise<void> {
    return native().downloadFile(url, headers, destinationPath);
}

/** Creates a directory and its missing parents. */
export function ensureDirectory(path: string): Promise<void> {
    return native().ensureDirectory(path);
}

/** Moves a file, replacing the destination when it exists. */
export function moveFile(fromPath: string, toPath: string): Promise<void> {
    return native().moveFile(fromPath, toPath);
}

/** Copies a file, replacing the destination when it exists. */
export function copyFile(fromPath: string, toPath: string): Promise<void> {
    return native().copyFile(fromPath, toPath);
}

/** Deletes a file; a file that is already gone is not an error. */
export function deleteFile(path: string): Promise<void> {
    return native().deleteFile(path);
}
