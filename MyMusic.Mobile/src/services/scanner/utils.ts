import { type ScanError } from './types';

export function fromEpochTimestamp(value: number | null | undefined): Date {
    if (!value) return new Date();
    const ms = value > 1e11 ? value : value * 1000;
    const d = new Date(ms);
    return isNaN(d.getTime()) ? new Date() : d;
}

/** The errors of a scan: each one reported is collected and passed to the `onError` of the scan options. */
export function createScanErrors(onError?: (path: string, error: string) => void): {
    errors: ScanError[];
    report: (path: string, error: string) => void;
} {
    const errors: ScanError[] = [];

    return {
        errors,
        report: (path, error) => {
            if (onError) {
                onError(path, error);
            }
            errors.push({ path, error });
        },
    };
}
