import {buildRepairDatesSummary, emptyRepairDatesResult, REPAIR_DATES_WRITE_CHUNK_SIZE, repairDates, type RepairDatesResult} from '../repair-dates';
import type {DeviceSongItem, FileModifiedAtSource, IFileOps, ISyncApiClient, ISyncConfig} from '../types';

/** The same cases as `RepairDatesServiceTests` in the CLI: both clients must make the same decisions. */
const DEVICE_ID = 7;
const REPOSITORY_PATH = '/music';
const SONG_PATH = 'Artist/song.mp3';

const CREATED_AT = new Date('2019-07-15T08:30:00Z');
const MODIFIED_AT = new Date('2024-03-01T10:00:00Z');
const LAST_SYNCED_AT = new Date('2025-01-01T12:00:00Z');

function song(overrides: Partial<DeviceSongItem> = {}): DeviceSongItem {
    return {
        songId: 1,
        path: SONG_PATH,
        serverCreatedAt: CREATED_AT.toISOString(),
        serverModifiedAt: MODIFIED_AT.toISOString(),
        lastSyncedModifiedAt: LAST_SYNCED_AT.toISOString(),
        ...overrides,
    };
}

function fullPath(relativePath: string): string {
    return `${REPOSITORY_PATH}/${relativePath}`;
}

function addMs(date: Date, ms: number): Date {
    return new Date(date.getTime() + ms);
}

function result(counts: Partial<RepairDatesResult>): RepairDatesResult {
    return {...emptyRepairDatesResult(), ...counts};
}

describe('repairDates', () => {
    // By full path: the files the listing finds, and the ones it leaves out (not a music extension)
    let songs: DeviceSongItem[];
    let files: Map<string, Date>;
    let unlistedFiles: Map<string, Date>;
    let failingPaths: Set<string>;
    let source: FileModifiedAtSource;
    let excludePatterns: string[];
    let setModificationTimes: jest.Mock;
    let onProgress: jest.Mock;

    beforeEach(() => {
        songs = [];
        files = new Map();
        unlistedFiles = new Map();
        failingPaths = new Set();
        source = 'ServerCreatedAt';
        excludePatterns = [];
        setModificationTimes = jest.fn(async (batch: Array<{path: string}>) =>
            batch.map(file => (failingPaths.has(file.path) ? 'denied' : null)));
        onProgress = jest.fn();
        jest.spyOn(console, 'warn').mockImplementation(() => {});
    });

    /** Every date set, over all the batches. */
    function datesSet(): Array<{path: string; modifiedAt: Date}> {
        return setModificationTimes.mock.calls.flatMap(([batch]) => batch);
    }

    afterEach(() => {
        jest.restoreAllMocks();
    });

    function run(dryRun = false): Promise<RepairDatesResult> {
        const apiClient = {
            getDeviceSongs: jest.fn(async (deviceId: number) => {
                expect(deviceId).toBe(DEVICE_ID);
                return {songs};
            }),
        } as unknown as ISyncApiClient;
        const config = {
            getDeviceId: () => DEVICE_ID,
            getRepositoryPath: () => REPOSITORY_PATH,
            getMusicExtensions: () => ['.mp3'],
            getExcludePatterns: () => excludePatterns,
            getFileModifiedAt: () => source,
        } as unknown as ISyncConfig;
        const fileOps = {
            resolveRepositoryPath: async (path: string) => path,
            listModificationTimes: async (rootPath: string, extensions: string[]) => {
                expect(rootPath).toBe(REPOSITORY_PATH);
                expect(extensions).toEqual(['.mp3']);
                return new Map([...files].map(([path, date]) => [path.slice(`${REPOSITORY_PATH}/`.length), date]));
            },
            fileExists: (path: string) => unlistedFiles.has(path),
            getModificationTime: (path: string) => unlistedFiles.get(path) ?? null,
            setModificationTimes,
        } as unknown as IFileOps;

        return repairDates({apiClient, config, fileOps}, dryRun, onProgress);
    }

    test.each<[FileModifiedAtSource, Date]>([
        ['ServerCreatedAt', CREATED_AT],
        ['ServerModifiedAt', MODIFIED_AT],
    ])('a file dated at the sync gets the configured server date (%s)', async (configured, expected) => {
        source = configured;
        songs = [song()];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({redated: 1}));
        expect(datesSet()).toEqual([{path: fullPath(SONG_PATH), modifiedAt: expected}]);
    });

    test('set to now, a file gets the date of its last sync', async () => {
        source = 'Now';
        songs = [song()];
        files.set(fullPath(SONG_PATH), CREATED_AT);

        expect(await run()).toEqual(result({redated: 1}));
        expect(datesSet()).toEqual([{path: fullPath(SONG_PATH), modifiedAt: LAST_SYNCED_AT}]);
    });

    test('a file without a song is skipped as not synced', async () => {
        songs = [song({songId: null})];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({notSynced: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a file marked for removal is skipped as not synced', async () => {
        songs = [song({action: 'Remove'})];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({notSynced: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a never synced file is skipped as not synced', async () => {
        // Also what an older server, which sends no dates, results in
        songs = [{songId: 1, path: SONG_PATH, action: 'Download'}];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({notSynced: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('an excluded path is skipped', async () => {
        excludePatterns = ['Artist/'];
        songs = [song()];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({excluded: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a file not on the device is skipped as missing', async () => {
        songs = [song()];

        expect(await run()).toEqual(result({missing: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a file edited on the device after the sync is skipped', async () => {
        // Re-dating it would hide the edit from the next sync
        songs = [song()];
        files.set(fullPath(SONG_PATH), addMs(LAST_SYNCED_AT, 1));

        expect(await run()).toEqual(result({editedLocally: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a last sync date with microseconds is compared at the precision of the file date', async () => {
        songs = [song({lastSyncedModifiedAt: '2025-01-01T12:00:00.0009999Z'})];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({redated: 1}));
    });

    test('a song changed on the server after the sync is skipped', async () => {
        // The next sync downloads it, and dates the file then
        source = 'ServerModifiedAt';
        songs = [song({serverModifiedAt: addMs(LAST_SYNCED_AT, 1).toISOString()})];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({changedOnServer: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a file that already has the date is skipped', async () => {
        songs = [song()];
        files.set(fullPath(SONG_PATH), CREATED_AT);

        expect(await run()).toEqual(result({upToDate: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a dry run counts the file without changing it', async () => {
        songs = [song()];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);

        expect(await run(true)).toEqual(result({redated: 1}));
        expect(setModificationTimes).not.toHaveBeenCalled();
    });

    test('a date that cannot be set counts a failure and goes on', async () => {
        const otherPath = 'Artist/other.mp3';
        songs = [song(), song({songId: 2, path: otherPath})];
        files.set(fullPath(SONG_PATH), LAST_SYNCED_AT);
        files.set(fullPath(otherPath), LAST_SYNCED_AT);
        failingPaths.add(fullPath(SONG_PATH));

        expect(await run()).toEqual(result({redated: 1, failed: 1}));
        expect(datesSet()).toContainEqual({path: fullPath(otherPath), modifiedAt: CREATED_AT});
    });

    test('a file the listing leaves out is looked up on its own', async () => {
        const flacPath = 'Artist/song.flac';
        songs = [song({path: flacPath})];
        unlistedFiles.set(fullPath(flacPath), LAST_SYNCED_AT);

        expect(await run()).toEqual(result({redated: 1}));
        expect(datesSet()).toEqual([{path: fullPath(flacPath), modifiedAt: CREATED_AT}]);
    });

    test('the dates are set in chunks, reporting the progress after each', async () => {
        const count = REPAIR_DATES_WRITE_CHUNK_SIZE * 2 + 1;
        songs = Array.from({length: count}, (_, i) => song({songId: i + 1, path: `Artist/${i}.mp3`}));
        songs.forEach(item => files.set(fullPath(item.path), LAST_SYNCED_AT));

        expect(await run()).toEqual(result({redated: count}));
        expect(setModificationTimes.mock.calls.map(([batch]) => batch.length))
            .toEqual([REPAIR_DATES_WRITE_CHUNK_SIZE, REPAIR_DATES_WRITE_CHUNK_SIZE, 1]);
        expect(onProgress.mock.calls).toEqual([
            [0, count],
            [REPAIR_DATES_WRITE_CHUNK_SIZE, count],
            [REPAIR_DATES_WRITE_CHUNK_SIZE * 2, count],
            [count, count],
        ]);
    });
});

describe('buildRepairDatesSummary', () => {
    test('lists only the skip reasons that happened', () => {
        expect(buildRepairDatesSummary(result({redated: 3, editedLocally: 2, missing: 1})))
            .toBe('3 re-dated, 3 skipped (2 edited locally, 1 missing)');
    });

    test('has no details when nothing was skipped', () => {
        expect(buildRepairDatesSummary(result({redated: 2}))).toBe('2 re-dated, 0 skipped');
    });
});
