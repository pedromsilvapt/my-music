/**
 * Whether `current` is later than `reference`, the way the server's sync compares file dates. Mirrors
 * `SyncDateComparison` in the CLI and `SyncComparisonHelper` in MyMusic.Common, which ignore the last digit of
 * the ticks (the database keeps microseconds). A `Date` only holds milliseconds, so this compares those: the
 * only difference allowed between the clients.
 */
export function isNewerThan(current: Date, reference: Date): boolean {
    return current.getTime() > reference.getTime();
}
