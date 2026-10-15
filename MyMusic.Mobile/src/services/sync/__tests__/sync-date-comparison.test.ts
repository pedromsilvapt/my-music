import {isNewerThan} from '../sync-date-comparison';

/**
 * The same vectors as `SyncDateComparisonTests` in the CLI and `SyncComparisonHelperSpecs` in MyMusic.Common,
 * at the millisecond precision of a `Date` instead of the database's microseconds.
 */
describe('isNewerThan', () => {
    const reference = new Date('2025-01-01T12:00:00Z');

    test('current strictly newer returns true', () => {
        expect(isNewerThan(new Date(reference.getTime() + 1000), reference)).toBe(true);
    });

    test('current older returns false', () => {
        expect(isNewerThan(new Date(reference.getTime() - 1000), reference)).toBe(false);
    });

    test('equal timestamps return false', () => {
        expect(isNewerThan(reference, new Date(reference.getTime()))).toBe(false);
    });

    test('within the precision returns false', () => {
        // A server date with microseconds is parsed down to the millisecond
        expect(isNewerThan(new Date('2025-01-01T12:00:00.0009999Z'), reference)).toBe(false);
    });

    test('beyond the precision returns true', () => {
        expect(isNewerThan(new Date(reference.getTime() + 1), reference)).toBe(true);
    });
});
