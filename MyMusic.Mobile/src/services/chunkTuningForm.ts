import type { ChunkSizeRange, SyncChunkTuning } from './sync/types';

/** The values of the "Sync Performance" settings screen, as typed. */
export interface ChunkTuningForm {
    adaptive: boolean;
    targetRequestSeconds: string;
    check: ChunkSizeRangeForm;
    resolve: ChunkSizeRangeForm;
}

export interface ChunkSizeRangeForm {
    size: string;
    min: string;
    max: string;
}

export type ChunkTuningFormErrors = Partial<Record<
    'targetRequestSeconds' | 'checkSize' | 'checkMin' | 'checkMax' | 'resolveSize' | 'resolveMin' | 'resolveMax',
    string
>>;

export function toChunkTuningForm(tuning: SyncChunkTuning): ChunkTuningForm {
    const range = (r: ChunkSizeRange): ChunkSizeRangeForm => ({ size: String(r.size), min: String(r.min), max: String(r.max) });
    return {
        adaptive: tuning.adaptive,
        targetRequestSeconds: String(tuning.targetRequestMs / 1000),
        check: range(tuning.check),
        resolve: range(tuning.resolve),
    };
}

/**
 * Reads the chunk sizes typed in the form. With fixed sizes only the sizes are asked for, so the range
 * and the target time keep the values in `current`.
 */
export function parseChunkTuningForm(
    form: ChunkTuningForm,
    current: SyncChunkTuning
): { tuning: SyncChunkTuning; errors?: undefined } | { tuning?: undefined; errors: ChunkTuningFormErrors } {
    const errors: ChunkTuningFormErrors = {};

    const parseRange = (range: ChunkSizeRangeForm, currentRange: ChunkSizeRange, key: 'check' | 'resolve'): ChunkSizeRange => {
        const size = parseCount(range.size);
        if (size === null) {
            errors[`${key}Size`] = 'Enter a whole number, 1 or more';
        }

        if (!form.adaptive) {
            return { ...currentRange, size: size ?? currentRange.size };
        }

        const min = parseCount(range.min);
        const max = parseCount(range.max);
        if (min === null) {
            errors[`${key}Min`] = 'Enter a whole number, 1 or more';
        } else if (size !== null && min > size) {
            errors[`${key}Min`] = 'Cannot be above the starting size';
        }
        if (max === null) {
            errors[`${key}Max`] = 'Enter a whole number, 1 or more';
        } else if (size !== null && max < size) {
            errors[`${key}Max`] = 'Cannot be below the starting size';
        }

        return { size: size ?? currentRange.size, min: min ?? currentRange.min, max: max ?? currentRange.max };
    };

    let targetRequestMs = current.targetRequestMs;
    if (form.adaptive) {
        const seconds = Number(form.targetRequestSeconds.trim().replace(',', '.'));
        if (form.targetRequestSeconds.trim() === '' || !Number.isFinite(seconds) || seconds <= 0) {
            errors.targetRequestSeconds = 'Enter a number of seconds above 0';
        } else {
            targetRequestMs = Math.round(seconds * 1000);
        }
    }

    const tuning: SyncChunkTuning = {
        adaptive: form.adaptive,
        targetRequestMs,
        check: parseRange(form.check, current.check, 'check'),
        resolve: parseRange(form.resolve, current.resolve, 'resolve'),
    };

    return Object.keys(errors).length > 0 ? { errors } : { tuning };
}

function parseCount(text: string): number | null {
    return /^\d+$/.test(text.trim()) && Number(text) >= 1 ? Number(text) : null;
}
