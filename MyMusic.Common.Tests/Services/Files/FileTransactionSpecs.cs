using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Files;

public class FileTransactionSpecs
{
    private const string SongPath = "/data/admin/Song.mp3";

    private const string OtherSongPath = "/data/admin/Other/Song.mp3";

    private const string TempFolder = "/data/.temp";

    private readonly FailingCommitInterceptor _failingCommit = new();

    private readonly Scenario _scenario;

    public FileTransactionSpecs()
    {
        _scenario = new Scenario(_failingCommit);
    }

    private CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PrepareOverwrite_NewPath_RollbackDeletesTheWrittenFile()
    {
        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        // A new file is written at a free path
        await files.PrepareOverwriteAsync(SongPath, CancellationToken);
        WriteFile(SongPath, "new");

        // Rolling back should remove it, and leave no backups behind
        await dbTransaction.RollbackAsync(CancellationToken);

        Files().ShouldBeEmpty();
    }

    [Fact]
    public async Task PrepareOverwrite_ExistingFile_RollbackRestoresTheOriginal()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        // The existing file is set aside, and a new one is written in its place
        await files.PrepareOverwriteAsync(SongPath, CancellationToken);
        _scenario.FileSystem.File.Exists(SongPath).ShouldBeFalse();
        WriteFile(SongPath, "new");

        // Rolling back should bring the original content back
        await dbTransaction.RollbackAsync(CancellationToken);

        ReadFile(SongPath).ShouldBe("original");
        Files().ShouldBe([SongPath]);
    }

    [Fact]
    public async Task PrepareEdit_RollbackRestoresTheOriginal()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        // The file is edited in place, so it should still be readable after being prepared
        await files.PrepareEditAsync(SongPath, CancellationToken);
        ReadFile(SongPath).ShouldBe("original");
        WriteFile(SongPath, "edited");

        // Rolling back should bring the original content back
        await dbTransaction.RollbackAsync(CancellationToken);

        ReadFile(SongPath).ShouldBe("original");
        Files().ShouldBe([SongPath]);
    }

    [Fact]
    public async Task EditThenMove_RollbackRestoresTheOriginalAtItsPath()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        // The file is edited, then moved to a folder that does not exist yet
        await files.PrepareEditAsync(SongPath, CancellationToken);
        WriteFile(SongPath, "edited");
        await files.MoveAsync(SongPath, OtherSongPath, CancellationToken);
        ReadFile(OtherSongPath).ShouldBe("edited");

        // Rolling back should undo both, newest first
        await dbTransaction.RollbackAsync(CancellationToken);

        ReadFile(SongPath).ShouldBe("original");
        Files().ShouldBe([SongPath]);
    }

    [Fact]
    public async Task Move_TargetExists_ThrowsWithoutOverwriting()
    {
        WriteFile(SongPath, "song");
        WriteFile(OtherSongPath, "other");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        await Should.ThrowAsync<IOException>(() => files.MoveAsync(SongPath, OtherSongPath, CancellationToken));

        ReadFile(OtherSongPath).ShouldBe("other");
    }

    [Fact]
    public async Task Delete_RollbackRestoresTheFile()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        await files.DeleteAsync(SongPath, CancellationToken);
        _scenario.FileSystem.File.Exists(SongPath).ShouldBeFalse();

        await dbTransaction.RollbackAsync(CancellationToken);

        ReadFile(SongPath).ShouldBe("original");
    }

    [Fact]
    public async Task Commit_KeepsTheChangesAndDeletesTheBackups()
    {
        WriteFile(SongPath, "original");

        await using (var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken))
        {
            await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

            // The file is overwritten, and a backup of the original is kept meanwhile
            await files.PrepareOverwriteAsync(SongPath, CancellationToken);
            WriteFile(SongPath, "new");
            TransactionFolders().ShouldHaveSingleItem();

            await dbTransaction.CommitAsync(CancellationToken);
        }

        // The new content should stay, and the backup be gone
        ReadFile(SongPath).ShouldBe("new");
        TransactionFolders().ShouldBeEmpty();
    }

    [Fact]
    public async Task CommitFails_UndoesTheChanges()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        await files.PrepareOverwriteAsync(SongPath, CancellationToken);
        WriteFile(SongPath, "new");

        // A failed commit counts as a rollback
        _failingCommit.Armed = true;
        await Should.ThrowAsync<InvalidOperationException>(() => dbTransaction.CommitAsync(CancellationToken));

        ReadFile(SongPath).ShouldBe("original");
        TransactionFolders().ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposedWithoutCommit_UndoesTheChanges()
    {
        WriteFile(SongPath, "original");

        await using (await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken))
        {
            await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

            await files.PrepareOverwriteAsync(SongPath, CancellationToken);
            WriteFile(SongPath, "new");

            // The transaction is abandoned, e.g. by an exception, without an explicit rollback
        }

        ReadFile(SongPath).ShouldBe("original");
        TransactionFolders().ShouldBeEmpty();
    }

    [Fact]
    public async Task UndoFails_KeepsTheBackups()
    {
        WriteFile(SongPath, "original");
        WriteFile(OtherSongPath, "other original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        // Two files are overwritten, and then something outside the transaction deletes the second one's backup
        await files.PrepareOverwriteAsync(SongPath, CancellationToken);
        await files.PrepareOverwriteAsync(OtherSongPath, CancellationToken);
        var transactionFolder = TransactionFolders().Single();
        _scenario.FileSystem.File.Delete(_scenario.FileSystem.Path.Combine(transactionFolder, "2"));

        await dbTransaction.RollbackAsync(CancellationToken);

        // The rest should still be undone, while the backups folder is kept for manual recovery
        ReadFile(SongPath).ShouldBe("original");
        TransactionFolders().ShouldBe([transactionFolder]);
        ReadFile(_scenario.FileSystem.Path.Combine(transactionFolder, "backups.txt"))
            .ShouldContain(OtherSongPath);
    }

    [Fact]
    public async Task Backups_AreIgnoredByRepositoryScans()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        await files.DeleteAsync(SongPath, CancellationToken);

        _scenario.FileSystem.File.Exists($"{TempFolder}/{MusicService.MusicIgnoreFile}").ShouldBeTrue();
    }

    [Fact]
    public async Task Operations_LockEachPathOnce()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        // The edit locks the file, and the move then only needs to lock its new path
        await files.PrepareEditAsync(SongPath, CancellationToken);
        await files.MoveAsync(SongPath, OtherSongPath, CancellationToken);

        _scenario.AdvisoryLocks.Acquisitions.ShouldBe([[FileKey(SongPath)], [FileKey(OtherSongPath)]]);
    }

    [Fact]
    public async Task Move_LocksBothPathsAtOnce()
    {
        WriteFile(SongPath, "original");

        await using var dbTransaction = await _scenario.DbContext.Database.BeginTransactionAsync(CancellationToken);
        await using var files = _scenario.FileTransactions.Begin(_scenario.DbContext);

        await files.MoveAsync(SongPath, OtherSongPath, CancellationToken);

        _scenario.AdvisoryLocks.Acquisitions.ShouldHaveSingleItem()
            .ShouldBe(AdvisoryLockKey.Normalize([FileKey(SongPath), FileKey(OtherSongPath)]));
    }

    [Fact]
    public void Begin_WithoutDatabaseTransaction_Throws()
    {
        Should.Throw<InvalidOperationException>(() => _scenario.FileTransactions.Begin(_scenario.DbContext));
    }

    private static AdvisoryLockKey FileKey(string path) => AdvisoryLockKey.Create(AdvisoryLockScope.File, 0, path);

    private void WriteFile(string path, string content)
    {
        _scenario.FileSystem.Directory.CreateDirectory(_scenario.FileSystem.Path.GetDirectoryName(path)!);
        _scenario.FileSystem.File.WriteAllText(path, content);
    }

    private string ReadFile(string path) => _scenario.FileSystem.File.ReadAllText(path);

    /// <summary>The files of the repository, outside of the <c>.temp</c> folder.</summary>
    private List<string> Files() =>
        _scenario.FileSystem.Directory.Exists("/data")
            ? _scenario.FileSystem.Directory.GetFiles("/data", "*", SearchOption.AllDirectories)
                .Where(path => !path.StartsWith(TempFolder))
                .ToList()
            : [];

    private List<string> TransactionFolders() =>
        _scenario.FileSystem.Directory.Exists(TempFolder)
            ? _scenario.FileSystem.Directory.GetDirectories(TempFolder, "tx-*").ToList()
            : [];
}
