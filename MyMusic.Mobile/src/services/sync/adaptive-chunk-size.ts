import type { ChunkSizeRange, SyncChunkTuning } from './types';

/** The chunk sizes of a new installation (see "Request Chunks" in docs/development/sync.md). Same as the CLI's. */
export const DEFAULT_CHUNK_TUNING: SyncChunkTuning = {
    adaptive: true,
    targetRequestMs: 500,
    check: { size: 50, min: 10, max: 1000 },
    resolve: { size: 200, min: 25, max: 1000 },
};

/**
 * The number of items to send in the next request of a chunked step of the sync (see "Request Chunks" in
 * docs/development/sync.md). It starts at the configured size and, when adaptive, follows how long the
 * server takes to answer: a full request answered in under half the target time doubles the size, and one
 * that takes longer than the target shrinks it in proportion (to half at most), always inside the
 * configured range. Mirrors the CLI's AdaptiveChunkSize.
 */
export class AdaptiveChunkSize {
    private readonly adaptive: boolean;
    private readonly targetMs: number;
    private readonly min: number;
    private readonly max: number;
    private size: number;

    constructor(range: ChunkSizeRange, tuning: SyncChunkTuning) {
        // A size outside the range widens it, so the configured size is always where the sync starts
        this.size = Math.max(1, Math.floor(range.size));
        this.min = Math.min(Math.max(Math.floor(range.min), 1), this.size);
        this.max = Math.max(Math.floor(range.max), this.size);
        this.targetMs = tuning.targetRequestMs;
        this.adaptive = tuning.adaptive && this.targetMs > 0;
    }

    /** The number of items the next request should carry. */
    get current(): number {
        return this.size;
    }

    /** Takes into account a request that carried `itemCount` items and was answered in `elapsedMs`. */
    report(itemCount: number, elapsedMs: number): void {
        if (!this.adaptive || itemCount <= 0) {
            return;
        }

        if (elapsedMs > this.targetMs) {
            const ideal = Math.floor(itemCount * (this.targetMs / elapsedMs));
            this.size = this.clamp(Math.max(ideal, Math.floor(this.size / 2)));
        }
        // A request with fewer items than the size (the last one) says little about a bigger one
        else if (itemCount >= this.size && elapsedMs < this.targetMs / 2) {
            this.size = this.clamp(this.size * 2);
        }
    }

    private clamp(size: number): number {
        return Math.min(Math.max(size, this.min), this.max);
    }
}
