import {createExclusionMatcher, type ExclusionMatcher} from './exclusions';
import {getFileModifiedAt} from './sync-actions-device';
import {isNewerThan} from './sync-date-comparison';
import type {DeviceSongItem, FileModifiedAtSource, IFileOps, ISyncApiClient, ISyncConfig} from './types';

/** How many files of the device a date repair re-dated, and how many it left alone, per reason. */
export interface RepairDatesResult {
    /** Files given the configured date (or that would be, in a dry run). */
    redated: number;
    /** Files with no song, marked for removal, or never synced. */
    notSynced: number;
    /** Files whose path matches an exclusion rule. */
    excluded: number;
    /** Files that are not on the device. */
    missing: number;
    /** Files changed on the device since the last sync. */
    editedLocally: number;
    /** Files whose song changed on the server since the last sync. */
    changedOnServer: number;
    /** Files that already have the configured date. */
    upToDate: number;
    /** Files whose date could not be set. */
    failed: number;
}

type Outcome = keyof RepairDatesResult;

export interface RepairDatesDeps {
    apiClient: ISyncApiClient;
    config: ISyncConfig;
    fileOps: IFileOps;
}

export function emptyRepairDatesResult(): RepairDatesResult {
    return {redated: 0, notSynced: 0, excluded: 0, missing: 0, editedLocally: 0, changedOnServer: 0, upToDate: 0, failed: 0};
}

export function skippedCount(result: RepairDatesResult): number {
    return result.notSynced + result.excluded + result.missing + result.editedLocally + result.changedOnServer + result.upToDate;
}

/**
 * The summary of a date repair, the same text the CLI's `repair-dates` prints, e.g.
 * "3 re-dated, 3 skipped (2 edited locally, 1 missing)".
 */
export function buildRepairDatesSummary(result: RepairDatesResult): string {
    const summary = `${result.redated} re-dated, ${skippedCount(result)} skipped`;

    const reasons: Array<[number, string]> = [
        [result.upToDate, 'already dated'],
        [result.editedLocally, 'edited locally'],
        [result.changedOnServer, 'changed on server'],
        [result.missing, 'missing'],
        [result.excluded, 'excluded'],
        [result.notSynced, 'not synced'],
    ];
    const details = reasons.filter(([count]) => count > 0).map(([count, label]) => `${count} ${label}`);

    return details.length > 0 ? `${summary} (${details.join(', ')})` : summary;
}

/** Files whose date is set per native call: few calls, and a progress that still moves on a large library. */
export const REPAIR_DATES_WRITE_CHUNK_SIZE = 250;

/** A file to give a date to. */
interface Redate {
    path: string;
    fullPath: string;
    modifiedAt: Date;
}

/**
 * Gives the files already on the device the date the device is configured to give the files a sync downloads
 * (see "Repairing the Dates of Synced Files" in docs/development/sync.md), without running a sync. With
 * `dryRun`, no file is changed and the result counts what would be. Mirrors `RepairDatesService` in the CLI.
 *
 * The CLI reads and sets the date of each file in turn. Each would be a call into native code here, so the
 * dates of the folder are read in one listing, and the new ones set in chunks; `onProgress` is called after
 * each chunk.
 */
export async function repairDates(
    deps: RepairDatesDeps,
    dryRun: boolean,
    onProgress?: (done: number, total: number) => void
): Promise<RepairDatesResult> {
    const deviceId = deps.config.getDeviceId();
    if (deviceId === null) {
        throw new Error('No device is configured');
    }

    const repositoryPath = await deps.fileOps.resolveRepositoryPath(deps.config.getRepositoryPath());
    const source = deps.config.getFileModifiedAt();
    const exclusions = createExclusionMatcher(deps.config.getExcludePatterns());

    const response = await deps.apiClient.getDeviceSongs(deviceId);
    const listed = await deps.fileOps.listModificationTimes(repositoryPath, deps.config.getMusicExtensions());

    const result = emptyRepairDatesResult();
    const redates: Redate[] = [];
    for (const song of response.songs) {
        const decision = decide(deps.fileOps, listed, song, repositoryPath, source, exclusions);
        if (typeof decision === 'string') {
            result[decision]++;
        } else if (dryRun) {
            result.redated++;
        } else {
            redates.push(decision);
        }
    }

    if (dryRun) {
        return result;
    }

    onProgress?.(0, redates.length);
    for (let start = 0; start < redates.length; start += REPAIR_DATES_WRITE_CHUNK_SIZE) {
        const chunk = redates.slice(start, start + REPAIR_DATES_WRITE_CHUNK_SIZE);
        const errors = await deps.fileOps.setModificationTimes(
            chunk.map(redate => ({path: redate.fullPath, modifiedAt: redate.modifiedAt}))
        );

        chunk.forEach((redate, index) => {
            const error = errors[index];
            if (error) {
                console.warn(`Failed to set the modified date of ${redate.path}: ${error}`);
                result.failed++;
            } else {
                result.redated++;
            }
        });
        onProgress?.(start + chunk.length, redates.length);
    }

    return result;
}

/** What to do with a file: leave it alone, for a reason, or give it a date. */
function decide(
    fileOps: IFileOps,
    listed: Map<string, Date>,
    song: DeviceSongItem,
    repositoryPath: string,
    source: FileModifiedAtSource,
    exclusions: ExclusionMatcher
): Exclude<Outcome, 'redated' | 'failed'> | Redate {
    const lastSynced = parseDate(song.lastSyncedModifiedAt);
    if (song.songId === null || song.action === 'Remove' || !lastSynced) {
        return 'notSynced';
    }

    if (exclusions(song.path) !== null) {
        return 'excluded';
    }

    // A file the listing left out (not a music extension, an unreadable folder) is looked up on its own
    const fullPath = `${repositoryPath}/${song.path}`;
    const fileDate = listed.get(song.path)
        ?? (fileOps.fileExists(fullPath) ? fileOps.getModificationTime(fullPath) : null);
    if (!fileDate) {
        return 'missing';
    }

    // Re-dating a file edited on the device would hide the edit from the next sync
    if (isNewerThan(fileDate, lastSynced)) {
        return 'editedLocally';
    }

    // The time the sync wrote the file, for 'Now'. Without a date from the server, the file is taken as not synced
    const target = source === 'Now' ? lastSynced : getFileModifiedAt(song, source);
    if (!target) {
        return 'notSynced';
    }

    // The next sync downloads the song, and dates the file then. A date after the last sync would also make
    // the file look changed on the device
    if (isNewerThan(target, lastSynced)) {
        return 'changedOnServer';
    }

    if (!isNewerThan(fileDate, target) && !isNewerThan(target, fileDate)) {
        return 'upToDate';
    }

    return {path: song.path, fullPath, modifiedAt: target};
}

function parseDate(value: string | null | undefined): Date | null {
    const date = new Date(value ?? NaN);
    return isNaN(date.getTime()) ? null : date;
}
