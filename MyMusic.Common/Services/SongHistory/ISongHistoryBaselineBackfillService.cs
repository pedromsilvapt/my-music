namespace MyMusic.Common.Services.SongHistory;

/// <summary>
/// Records the <c>created</c> baseline revision of songs that existed before baselines did, so every song's history
/// starts the same way regardless of when it was added.
/// </summary>
public interface ISongHistoryBaselineBackfillService
{
    /// <summary>
    /// Records the baseline of up to <paramref name="batchSize"/> songs that still miss one. Returns how many such
    /// songs were found (zero once the backfill is complete) and how many got their baseline recorded; songs with
    /// history changes still waiting in the queue are skipped and picked up by a later batch.
    /// </summary>
    Task<(int candidates, int recorded)> BackfillBatchAsync(int batchSize, CancellationToken cancellationToken);
}
