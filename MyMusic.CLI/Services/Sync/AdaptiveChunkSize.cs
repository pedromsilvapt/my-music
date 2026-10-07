namespace MyMusic.CLI.Services.Sync;

using MyMusic.CLI.Services.Sync.Types;

/// <summary>
/// The number of items to send in the next request of a chunked step of the sync (see "Request Chunks" in
/// docs/development/sync.md). It starts at the configured size and, when adaptive, follows how long the
/// server takes to answer: a full request answered in under half the target time doubles the size, and one
/// that takes longer than the target shrinks it in proportion (to half at most), always inside the
/// configured range. Mirrors Mobile's <c>AdaptiveChunkSize</c>.
/// </summary>
public class AdaptiveChunkSize
{
    private readonly bool _adaptive;
    private readonly TimeSpan _target;
    private readonly int _min;
    private readonly int _max;

    public AdaptiveChunkSize(ChunkSizeRange range, SyncChunkTuning tuning)
    {
        // A size outside the range widens it, so the configured size is always where the sync starts
        Current = Math.Max(1, range.Size);
        _min = Math.Clamp(range.Min, 1, Current);
        _max = Math.Max(range.Max, Current);
        _target = tuning.TargetRequestDuration;
        _adaptive = tuning.Adaptive && _target > TimeSpan.Zero;
    }

    /// <summary>The number of items the next request should carry.</summary>
    public int Current { get; private set; }

    /// <summary>
    /// Takes into account a request that carried <paramref name="itemCount"/> items and was answered in
    /// <paramref name="elapsed"/>.
    /// </summary>
    public void Report(int itemCount, TimeSpan elapsed)
    {
        if (!_adaptive || itemCount <= 0)
        {
            return;
        }

        if (elapsed > _target)
        {
            var ideal = (int)(itemCount * (_target / elapsed));
            Current = Math.Clamp(Math.Max(ideal, Current / 2), _min, _max);
        }
        // A request with fewer items than the size (the last one) says little about a bigger one
        else if (itemCount >= Current && elapsed < _target / 2)
        {
            Current = Math.Clamp(Current * 2, _min, _max);
        }
    }
}
