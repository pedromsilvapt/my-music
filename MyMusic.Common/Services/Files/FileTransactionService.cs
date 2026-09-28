using System.Data.Common;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.Services;

/// <summary>
///     <see cref="IFileTransactionService"/> whose transactions follow their database transaction through
///     <see cref="FileTransactionInterceptor"/>. Must be registered as a singleton, shared with that interceptor.
/// </summary>
public class FileTransactionService(
    IFileSystem fileSystem,
    IOptions<Config> config,
    IAdvisoryLockService advisoryLockService,
    ILogger<FileTransactionService> logger) : IFileTransactionService
{
    private readonly ConditionalWeakTable<DbTransaction, FileTransaction> _transactions = new();

    public IFileTransaction Begin(MusicDbContext db)
    {
        var dbTransaction = db.Database.CurrentTransaction?.GetDbTransaction() ?? throw new InvalidOperationException(
            "File transactions require an active database transaction, whose rollback they follow.");

        // Without the interceptor, a committed transaction would look abandoned, and its file changes would be undone
        var interceptors = db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.Interceptors;

        if (interceptors?.OfType<FileTransactionInterceptor>().Any() != true)
        {
            throw new InvalidOperationException(
                $"File transactions require the {nameof(FileTransactionInterceptor)} to be registered on the context.");
        }

        var tempFolder = fileSystem.Path.Combine(config.Value.MusicRepositoryPath, ".temp");

        var transaction = new FileTransaction(db, fileSystem, advisoryLockService, tempFolder, logger,
            () => _transactions.Remove(dbTransaction));

        if (!_transactions.TryAdd(dbTransaction, transaction))
        {
            throw new InvalidOperationException("The database transaction already has a file transaction.");
        }

        return transaction;
    }

    /// <summary>The file transaction bound to <paramref name="dbTransaction"/>, if any.</summary>
    internal FileTransaction? Find(DbTransaction dbTransaction) =>
        _transactions.TryGetValue(dbTransaction, out var transaction) ? transaction : null;
}

/// <summary>
///     Journal of a transaction's file changes. Its database transaction ends it through
///     <see cref="MarkCommitted"/> or <see cref="Undo"/>; disposing it before either happened undoes it too.
/// </summary>
internal sealed class FileTransaction(
    MusicDbContext db,
    IFileSystem fileSystem,
    IAdvisoryLockService advisoryLockService,
    string tempFolder,
    ILogger logger,
    Action onDisposed) : IFileTransaction
{
    /// <summary>Lists each backup's original path, for manual recovery when a rollback could not restore it.</summary>
    private const string BackupsIndexFile = "backups.txt";

    private readonly string _folder = fileSystem.Path.Combine(tempFolder, $"tx-{Guid.NewGuid()}");

    private readonly List<Step> _steps = [];

    private readonly HashSet<AdvisoryLockKey> _heldLocks = [];

    private readonly List<IAsyncDisposable> _lockHandles = [];

    private int _backupCount;

    private State _state = State.Active;

    public async Task PrepareOverwriteAsync(string path, CancellationToken cancellationToken = default)
    {
        await LockAsync([path], cancellationToken);

        string? backup = null;

        if (fileSystem.File.Exists(path))
        {
            backup = NextBackupPath(path);
            fileSystem.File.Move(path, backup);
        }

        _steps.Add(new Overwrite(path, backup));
    }

    public async Task PrepareEditAsync(string path, CancellationToken cancellationToken = default)
    {
        await LockAsync([path], cancellationToken);

        var backup = NextBackupPath(path);
        fileSystem.File.Copy(path, backup);

        _steps.Add(new Edit(path, backup));
    }

    public async Task MoveAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        await LockAsync([from, to], cancellationToken);

        fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(to)!);
        fileSystem.File.Move(from, to);

        _steps.Add(new Move(from, to));
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        await LockAsync([path], cancellationToken);

        var backup = NextBackupPath(path);
        fileSystem.File.Move(path, backup);

        _steps.Add(new Delete(path, backup));
    }

    /// <summary>Keeps the changes: the backups are deleted once this transaction is disposed.</summary>
    internal void MarkCommitted()
    {
        if (_state == State.Active)
        {
            _state = State.Committed;
        }
    }

    /// <summary>
    ///     Undoes every change, newest first. Runs at most once. A step that fails is logged and skipped, and the
    ///     backups are then kept, since they may be the only copy of the originals.
    /// </summary>
    internal void Undo()
    {
        if (_state != State.Active)
        {
            return;
        }

        _state = State.RolledBack;

        var failed = false;

        for (var i = _steps.Count - 1; i >= 0; i--)
        {
            try
            {
                _steps[i].Undo(fileSystem);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed = true;
                logger.LogError(ex, "Failed to undo {Step} of a rolled back transaction", _steps[i]);
            }
        }

        if (failed)
        {
            logger.LogError("Kept the backups of a rolled back transaction that could not be fully undone in {Folder}",
                _folder);
        }
        else
        {
            DeleteFolder();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_state == State.Disposed)
        {
            return;
        }

        // Ended without a commit or a rollback: the database transaction is being abandoned, which rolls it back
        Undo();

        if (_state == State.Committed)
        {
            DeleteFolder();
        }

        _state = State.Disposed;
        onDisposed();

        foreach (var handle in _lockHandles)
        {
            await handle.DisposeAsync();
        }
    }

    /// <summary>
    ///     Locks the paths for the rest of the database transaction, so no concurrent transaction touches them before
    ///     this one's changes are either committed or undone.
    /// </summary>
    private async Task LockAsync(string[] paths, CancellationToken cancellationToken)
    {
        if (_state != State.Active)
        {
            throw new InvalidOperationException("The file transaction has already ended.");
        }

        // Keys already held are skipped: not every lock implementation is reentrant
        var keys = paths
            .Select(path => AdvisoryLockKey.Create(AdvisoryLockScope.File, 0, fileSystem.Path.GetFullPath(path)))
            .Where(key => !_heldLocks.Contains(key))
            .ToList();

        if (keys.Count == 0)
        {
            return;
        }

        _lockHandles.Add(await advisoryLockService.AcquireTransactionLocksAsync(db, keys, cancellationToken));
        _heldLocks.UnionWith(keys);
    }

    private string NextBackupPath(string originalPath)
    {
        if (_backupCount == 0)
        {
            fileSystem.Directory.CreateDirectory(_folder);

            // Keeps repository scans out of the backups
            var ignoreFile = fileSystem.Path.Combine(tempFolder, MusicService.MusicIgnoreFile);

            if (!fileSystem.File.Exists(ignoreFile))
            {
                fileSystem.File.WriteAllText(ignoreFile, "");
            }
        }

        var name = $"{++_backupCount}";

        fileSystem.File.AppendAllText(fileSystem.Path.Combine(_folder, BackupsIndexFile),
            $"{name}\t{originalPath}{Environment.NewLine}");

        return fileSystem.Path.Combine(_folder, name);
    }

    private void DeleteFolder()
    {
        try
        {
            if (fileSystem.Directory.Exists(_folder))
            {
                fileSystem.Directory.Delete(_folder, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Failed to delete the backups folder {Folder} of a file transaction", _folder);
        }
    }

    private enum State
    {
        Active,
        Committed,
        RolledBack,
        Disposed,
    }

    private abstract record Step
    {
        public abstract void Undo(IFileSystem fileSystem);
    }

    private sealed record Overwrite(string Path, string? Backup) : Step
    {
        public override void Undo(IFileSystem fileSystem)
        {
            if (fileSystem.File.Exists(Path))
            {
                fileSystem.File.Delete(Path);
            }

            if (Backup is not null)
            {
                fileSystem.File.Move(Backup, Path);
            }
        }
    }

    private sealed record Edit(string Path, string Backup) : Step
    {
        public override void Undo(IFileSystem fileSystem) => fileSystem.File.Move(Backup, Path, overwrite: true);
    }

    private sealed record Move(string From, string To) : Step
    {
        public override void Undo(IFileSystem fileSystem)
        {
            if (fileSystem.File.Exists(To))
            {
                fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(From)!);
                fileSystem.File.Move(To, From);
            }
        }
    }

    private sealed record Delete(string Path, string Backup) : Step
    {
        public override void Undo(IFileSystem fileSystem)
        {
            fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(Path)!);
            fileSystem.File.Move(Backup, Path);
        }
    }
}
