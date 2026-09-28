using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MyMusic.Common.Tests.Utilities;

/// <summary>
///     Once armed, fails every transaction commit, which happens after all of a unit of work's other steps (including
///     its file changes) are done.
/// </summary>
public sealed class FailingCommitInterceptor : DbTransactionInterceptor
{
    public bool Armed { get; set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        Armed
            ? throw new InvalidOperationException("Commit failed")
            : base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
}
