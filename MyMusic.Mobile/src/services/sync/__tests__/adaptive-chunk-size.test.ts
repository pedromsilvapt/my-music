import { AdaptiveChunkSize, DEFAULT_CHUNK_TUNING } from '../adaptive-chunk-size';
import type { ChunkSizeRange } from '../types';

const TARGET_MS = 2000;

function create(range: ChunkSizeRange): AdaptiveChunkSize {
    return new AdaptiveChunkSize(range, { ...DEFAULT_CHUNK_TUNING, adaptive: true, targetRequestMs: TARGET_MS });
}

describe('AdaptiveChunkSize', () => {
    test('starts at the configured size', () => {
        expect(create({ size: 50, min: 10, max: 1000 }).current).toBe(50);
    });

    test('a full request under half the target doubles the size', () => {
        const chunkSize = create({ size: 50, min: 10, max: 1000 });

        chunkSize.report(50, 999);

        expect(chunkSize.current).toBe(100);
    });

    test('a request between half the target and the target keeps the size', () => {
        const chunkSize = create({ size: 50, min: 10, max: 1000 });

        chunkSize.report(50, 1000);
        chunkSize.report(50, 2000);

        expect(chunkSize.current).toBe(50);
    });

    test('a fast request with fewer items than the size keeps the size', () => {
        // The last request of a step is rarely full: it says little about a bigger one
        const chunkSize = create({ size: 50, min: 10, max: 1000 });

        chunkSize.report(49, 1);

        expect(chunkSize.current).toBe(50);
    });

    test('a request over the target shrinks the size in proportion', () => {
        const chunkSize = create({ size: 100, min: 10, max: 1000 });

        chunkSize.report(100, 2500);

        expect(chunkSize.current).toBe(80);
    });

    test('a request far over the target halves the size at most', () => {
        const chunkSize = create({ size: 100, min: 10, max: 1000 });

        chunkSize.report(100, 60_000);

        expect(chunkSize.current).toBe(50);
    });

    test('never leaves the range', () => {
        const chunkSize = create({ size: 50, min: 40, max: 60 });

        chunkSize.report(50, 1);
        expect(chunkSize.current).toBe(60);

        chunkSize.report(60, 60_000);
        chunkSize.report(40, 60_000);
        expect(chunkSize.current).toBe(40);
    });

    test('keeps the size when not adaptive', () => {
        const chunkSize = new AdaptiveChunkSize(
            { size: 50, min: 10, max: 1000 },
            { ...DEFAULT_CHUNK_TUNING, adaptive: false, targetRequestMs: TARGET_MS });

        chunkSize.report(50, 1);
        chunkSize.report(50, 60_000);

        expect(chunkSize.current).toBe(50);
    });

    test.each([
        [{ size: 5, min: 10, max: 1000 }, 5, 1000, 5],
        [{ size: 2000, min: 10, max: 1000 }, 2000, 2000, 10],
        [{ size: 0, min: 0, max: 0 }, 1, 1, 1],
    ])('a size outside the range widens the range to it: %j', (range, expectedStart, expectedMax, expectedMin) => {
        const chunkSize = create(range);
        expect(chunkSize.current).toBe(expectedStart);

        // Growing reaches the widened maximum, shrinking the widened minimum
        for (let i = 0; i < 20; i++) {
            chunkSize.report(chunkSize.current, 1);
        }
        expect(chunkSize.current).toBe(expectedMax);

        for (let i = 0; i < 20; i++) {
            chunkSize.report(chunkSize.current, 60_000);
        }
        expect(chunkSize.current).toBe(expectedMin);
    });
});
