using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MyMusic.Common.Services;

/// <summary>
///     Ends each <see cref="IFileTransaction"/> along with its database transaction: a commit keeps its changes, while
///     a rollback or a failed commit undoes them. Rollbacks are undone before the database rolls back, while the
///     transaction still holds the locks on the files.
/// </summary>
public class FileTransactionInterceptor(FileTransactionService fileTransactions) : DbTransactionInterceptor
{
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        fileTransactions.Find(transaction)?.MarkCommitted();

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);

        return Task.CompletedTask;
    }

    public override InterceptionResult TransactionRollingBack(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result)
    {
        fileTransactions.Find(transaction)?.Undo();

        return result;
    }

    public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(TransactionRollingBack(transaction, eventData, result));

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
    {
        // A failed commit is treated as a rollback
        if (eventData.Action is "Commit" or "Rollback")
        {
            fileTransactions.Find(transaction)?.Undo();
        }
    }

    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TransactionFailed(transaction, eventData);

        return Task.CompletedTask;
    }
}
