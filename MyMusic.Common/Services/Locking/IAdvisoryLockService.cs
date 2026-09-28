namespace MyMusic.Common.Services;

/// <summary>
///     Takes exclusive locks that span a database transaction. They protect find-or-create logic that no unique
///     constraint can protect (e.g. artists, whose names are not unique) against concurrent requests, even across
///     server instances.
/// </summary>
public interface IAdvisoryLockService
{
    /// <summary>
    ///     Waits until every key is held by the current transaction of <paramref name="db"/>. Keys are acquired in the
    ///     canonical order of <see cref="AdvisoryLockKey.Normalize"/>, so concurrent callers cannot deadlock on them.
    /// </summary>
    /// <returns>
    ///     A handle to dispose only after the transaction has been committed or rolled back. Implementations backed by
    ///     the database release the locks when the transaction ends and ignore the handle; in-process implementations
    ///     release them when the handle is disposed.
    /// </returns>
    Task<IAsyncDisposable> AcquireTransactionLocksAsync(MusicDbContext db, IEnumerable<AdvisoryLockKey> keys,
        CancellationToken cancellationToken = default);
}
