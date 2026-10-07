import { parseChunkTuningForm, toChunkTuningForm } from '../chunkTuningForm';
import { DEFAULT_CHUNK_TUNING } from '../sync/adaptive-chunk-size';

describe('chunk tuning form', () => {
    test('the form of a tuning parses back to it', () => {
        const form = toChunkTuningForm(DEFAULT_CHUNK_TUNING);

        expect(form).toMatchObject({ adaptive: true, targetRequestSeconds: '0.5', check: { size: '50', min: '10', max: '1000' } });
        expect(parseChunkTuningForm(form, DEFAULT_CHUNK_TUNING)).toEqual({ tuning: DEFAULT_CHUNK_TUNING });
    });

    test('reads the typed values', () => {
        const result = parseChunkTuningForm({
            adaptive: true,
            targetRequestSeconds: '1,5',
            check: { size: ' 100 ', min: '20', max: '400' },
            resolve: { size: '300', min: '300', max: '300' },
        }, DEFAULT_CHUNK_TUNING);

        expect(result.tuning).toEqual({
            adaptive: true,
            targetRequestMs: 1500,
            check: { size: 100, min: 20, max: 400 },
            resolve: { size: 300, min: 300, max: 300 },
        });
    });

    test('fixed sizes keep the range and the target time they had', () => {
        const result = parseChunkTuningForm({
            adaptive: false,
            targetRequestSeconds: 'not asked',
            check: { size: '5', min: '', max: '' },
            resolve: { size: '5000', min: '', max: '' },
        }, DEFAULT_CHUNK_TUNING);

        expect(result.tuning).toEqual({
            ...DEFAULT_CHUNK_TUNING,
            adaptive: false,
            check: { ...DEFAULT_CHUNK_TUNING.check, size: 5 },
            resolve: { ...DEFAULT_CHUNK_TUNING.resolve, size: 5000 },
        });
    });

    test('rejects values that are not counts, a range that leaves the size out and a target time of 0', () => {
        const result = parseChunkTuningForm({
            adaptive: true,
            targetRequestSeconds: '0',
            check: { size: '50', min: '60', max: '40' },
            resolve: { size: '0', min: '1.5', max: 'many' },
        }, DEFAULT_CHUNK_TUNING);

        expect(result.tuning).toBeUndefined();
        expect(Object.keys(result.errors ?? {}).sort()).toEqual(
            ['checkMax', 'checkMin', 'resolveMax', 'resolveMin', 'resolveSize', 'targetRequestSeconds']);
    });
});
