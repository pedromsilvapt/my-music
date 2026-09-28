namespace MyMusic.Common.Services;

/// <summary>
///     Caps how many songs a single user can import at the same time (see
///     <see cref="Config.MaxConcurrentImportsPerUser"/>). Correctness does not depend on it (that is the job of
///     <see cref="IAdvisoryLockService"/>); it bounds the database connections and disk I/O one user can tie up.
/// </summary>
public interface IUserImportThrottle
{
    /// <summary>
    ///     Waits for a free import slot for <paramref name="userId"/>. Dispose the result to free the slot.
    /// </summary>
    Task<IDisposable> AcquireAsync(long userId, CancellationToken cancellationToken = default);
}
