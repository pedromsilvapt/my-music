import type { ChunkSizeRange, SyncChunkTuning } from '../types';

/** Chunks that keep their size for the whole sync, whatever the requests take. */
export function fixedChunkTuning(checkSize = 50, resolveSize = 200): SyncChunkTuning {
    return {
        adaptive: false,
        targetRequestMs: 2000,
        check: { size: checkSize, min: checkSize, max: checkSize },
        resolve: { size: resolveSize, min: resolveSize, max: resolveSize },
    };
}

/** Chunks that follow the response times, starting at the given sizes. */
export function adaptiveChunkTuning(check: ChunkSizeRange, resolve: ChunkSizeRange = { size: 200, min: 25, max: 1000 }): SyncChunkTuning {
    return { adaptive: true, targetRequestMs: 2000, check, resolve };
}
