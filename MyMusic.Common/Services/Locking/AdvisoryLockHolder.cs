namespace MyMusic.Common.Services;

/// <summary>
///     Holds the handle of <see cref="IAdvisoryLockService.AcquireTransactionLocksAsync"/> until the transaction has
///     ended. Declare it with <c>await using</c> <b>before</b> the transaction, so it is disposed after it, and set
///     <see cref="Handle"/> once the locks are acquired inside the transaction.
/// </summary>
public sealed class AdvisoryLockHolder : IAsyncDisposable
{
    public IAsyncDisposable? Handle { get; set; }

    public ValueTask DisposeAsync() => Handle?.DisposeAsync() ?? ValueTask.CompletedTask;
}
