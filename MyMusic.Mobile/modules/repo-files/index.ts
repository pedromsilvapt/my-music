import { requireNativeModule } from 'expo';

/**
 * File operations on the music repository in shared storage, done natively with plain filesystem
 * paths (or `file://` URIs). expo-file-system rejects any operation whose target does not exist yet
 * outside the app sandbox, so it cannot create, replace or rename the repository files, and it needs
 * several native calls per file to list them, see {@link listFiles}.
 *
 * Android only. Writing needs the "All files access" permission, see {@link hasAllFilesAccess}.
 */

/** A file found by {@link listFiles}. The times are in milliseconds since the epoch. */
export interface ListedFile {
    /** Relative to the listed folder, with `/` separators. */
    relativePath: string;
    size: number;
    modifiedAt: number;
    /** Null when the device cannot tell it. */
    createdAt: number | null;
}

/** A folder or file that {@link listFiles} could not read. */
export interface ListFilesError {
    /** Relative to the listed folder, with `/` separators; empty for the listed folder itself. */
    path: string;
    error: string;
}

export interface ListFilesResult {
    files: ListedFile[];
    errors: ListFilesError[];
}

interface RepoFilesNativeModule {
    hasAllFilesAccess(): Promise<boolean>;
    requestAllFilesAccess(): Promise<void>;
    downloadFile(url: string, headers: Record<string, string>, destinationPath: string): Promise<void>;
    ensureDirectory(path: string): Promise<void>;
    moveFile(fromPath: string, toPath: string): Promise<void>;
    copyFile(fromPath: string, toPath: string): Promise<void>;
    deleteFile(path: string): Promise<void>;
    setModifiedTime(path: string, modifiedAt: number): Promise<void>;
    listFiles(rootPath: string, extensions: string[]): Promise<ListFilesResult>;
    resolveDirectoryPath(uri: string): Promise<string | null>;
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

/** Sets the modified time of a file, in milliseconds since the epoch. Rejects when the storage refuses it. */
export function setModifiedTime(path: string, modifiedAt: number): Promise<void> {
    return native().setModifiedTime(path, modifiedAt);
}

/**
 * Lists the files of a folder and its subfolders that have one of the extensions (with the dot, e.g.
 * `.mp3`), in a single native call. It knows nothing of the exclusion rules: the caller filters the
 * result. What cannot be read is returned as an error and does not stop the listing; a root that
 * is not a folder rejects.
 */
export function listFiles(rootPath: string, extensions: string[]): Promise<ListFilesResult> {
    return native().listFiles(rootPath, extensions);
}

/**
 * The filesystem path of a folder picked with the system folder picker (a `content://` tree URI),
 * resolved by Android from the storage volume the folder is in. Null when the folder has no such
 * path, as one of a cloud provider.
 */
export function resolveDirectoryPath(uri: string): Promise<string | null> {
    return native().resolveDirectoryPath(uri);
}
