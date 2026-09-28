using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Models;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using Npgsql;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

/// <summary>
///     Concurrent imports are made safe by advisory locks on everything a song may find-or-create or collide with.
///     SQLite has no advisory locks, so these specs use <see cref="InProcessAdvisoryLockService"/> and play the role of
///     the concurrent import themselves, by holding its keys.
/// </summary>
public class MusicServiceConcurrencySpecs
{
    private const string SongPath = "/music/Song.mp3";

    private const string RepositoryPath = "/data/admin/My Artist/My Album/Song - My Artist.mp3";

    private const string FeaturingRepositoryPath = "/data/admin/My Artist/My Album/Song - My Artist, Guest Artist.mp3";

    public enum LockedResource
    {
        Album,
        AlbumArtist,
        SongArtist,
        Checksum,
    }

    [Theory]
    [InlineData(LockedResource.Album)]
    [InlineData(LockedResource.AlbumArtist)]
    [InlineData(LockedResource.SongArtist)]
    [InlineData(LockedResource.Checksum)]
    public async Task ImportMusic_ResourceLockedByConcurrentImport_WaitsUntilReleased(LockedResource resource)
    {
        // Setup: a song by "My Artist" (album artist) featuring "Guest Artist"
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist", "Guest Artist"], ["Rock"]);

        var key = KeyFor(resource, scenario);

        // A concurrent import holds a key this song needs
        var concurrentImport = await scenario.AdvisoryLocks.HoldAsync(key);

        // The import should block on that key before writing anything
        var import = musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        await scenario.AdvisoryLocks.WaitingFor(key)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        import.IsCompleted.ShouldBeFalse();
        scenario.FileSystem.File.Exists(FeaturingRepositoryPath).ShouldBeFalse();

        // Once the concurrent import finishes, the import should resume and succeed
        await concurrentImport.DisposeAsync();
        await import.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        job.Exceptions.ShouldBeEmpty();
        scenario.DbContext.Songs.Select(s => s.RepositoryPath).ToList().ShouldBe([FeaturingRepositoryPath]);
    }

    [Fact]
    public async Task ImportMusic_UnrelatedResourceLocked_DoesNotWait()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        // A concurrent import holds another album of the same artist name, and an artist of another user
        await using var otherAlbum = await scenario.AdvisoryLocks.HoldAsync(
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, scenario.AdminUser.Id, "My Artist", "Other Album"));
        await using var otherUserArtist = await scenario.AdvisoryLocks.HoldAsync(
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, scenario.AdminUser.Id + 1, "My Artist"));

        // The import should not wait for either
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
                cancellationToken: TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        job.Exceptions.ShouldBeEmpty();
        scenario.DbContext.Songs.Count().ShouldBe(1);
    }

    [Fact]
    public async Task ImportMusic_SongsOfTheSameAlbum_ContendOnlyOnAlbumAndArtist()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, "/music/Song A.mp3", "Song A", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song B.mp3", "Song B", "My Album",
            ["My Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // Both songs should lock the shared album and artist, but not the shared genre (upserted lock-free),
        // and each should lock its own checksum (paths are protected by their unique index instead)
        var fileLocks = AdvisoryLockKey.Create(AdvisoryLockScope.File, 0, "").ClassId;
        var acquisitions = scenario.AdvisoryLocks.Acquisitions.Where(keys => keys[0].ClassId != fileLocks).ToList();
        acquisitions.Count.ShouldBe(2);
        acquisitions[0].Intersect(acquisitions[1]).ShouldBe([
            AdvisoryLockKey.Create(AdvisoryLockScope.Artist, scenario.AdminUser.Id, "My Artist"),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, scenario.AdminUser.Id, "My Artist", "My Album"),
        ], ignoreOrder: true);
        acquisitions[0].Count.ShouldBe(3);

        // Each song should then only lock its own file, while writing it
        scenario.AdvisoryLocks.Acquisitions.Where(keys => keys[0].ClassId == fileLocks)
            .Select(keys => keys.Single()).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task ImportMusic_ExistingGenre_IsReusedByTheUpsert()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        // The user already has the song's genre
        var existingGenre = new Genre { Name = "Rock", OwnerId = scenario.AdminUser.Id };
        scenario.DbContext.Add(existingGenre);
        await scenario.DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock", "Metal"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // The existing genre should be reused, and only the missing one created
        job.Exceptions.ShouldBeEmpty();
        scenario.DbContext.Genres.Select(g => g.Name).ToList().ShouldBe(["Rock", "Metal"], ignoreOrder: true);
        scenario.DbContext.SongGenres.Select(sg => sg.GenreId).ToList().ShouldContain(existingGenre.Id);
    }

    [Fact]
    public async Task ImportMusic_ConcurrencyConflictOnce_RetriesFromScratch()
    {
        // The second save of the first attempt (which claims the song's path) hits a deadlock with another writer
        var interceptor = new FailingSaveChangesInterceptor(call => call == 2 ? Deadlock() : null);
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // The retry should import the song as if nothing happened: no duplicates, no leftovers of the first attempt
        job.Exceptions.ShouldBeEmpty();
        interceptor.Calls.ShouldBe(4);
        scenario.DbContext.Songs.Select(s => s.RepositoryPath).ToList().ShouldBe([RepositoryPath]);
        scenario.DbContext.Albums.Select(a => a.Name).ToList().ShouldBe(["My Album"]);
        scenario.DbContext.Artists.Select(a => a.Name).ToList().ShouldBe(["My Artist"]);
        scenario.DbContext.Genres.Select(g => g.Name).ToList().ShouldBe(["Rock"]);
        RepositoryFiles(scenario).ShouldBe([RepositoryPath]);
        job.FileMapping.ShouldBe(new Dictionary<string, string> { [SongPath] = RepositoryPath });
    }

    [Fact]
    public async Task ImportMusic_ConcurrencyConflictEveryAttempt_GivesUpAndCleansUp()
    {
        // Every attempt deadlocks on its final save
        var interceptor = new FailingSaveChangesInterceptor(call => call % 2 == 0 ? Deadlock() : null);
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // After three attempts the failure should be reported, leaving nothing behind
        interceptor.Calls.ShouldBe(6);
        job.Exceptions.Count.ShouldBe(1);
        scenario.DbContext.Songs.ShouldBeEmpty();
        scenario.DbContext.Albums.ShouldBeEmpty();
        scenario.DbContext.Artists.ShouldBeEmpty();
        scenario.DbContext.Genres.ShouldBeEmpty();
        RepositoryFiles(scenario).ShouldBeEmpty();
        job.FileMapping.ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportMusic_NonConcurrencyFailure_IsNotRetried()
    {
        var interceptor = new FailingSaveChangesInterceptor(call =>
            call == 2 ? new InvalidOperationException("Something unrelated broke") : null);
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // The failure should be reported right away, without any file written
        interceptor.Calls.ShouldBe(2);
        job.Exceptions.Count.ShouldBe(1);
        scenario.DbContext.Songs.ShouldBeEmpty();
        RepositoryFiles(scenario).ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportMusic_FailedSong_DoesNotLeakIntoTheNextSong()
    {
        // The first song (Song A) fails after creating its album and artist; the second song (Song B) succeeds
        var interceptor = new FailingSaveChangesInterceptor(call =>
            call == 2 ? new InvalidOperationException("Something unrelated broke") : null);
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, "/music/Song A.mp3", "Song A", "Album A", ["Artist A"], ["Rock"]);

        var songB = new SongImportMetadata("/music/Song B.mp3", DateTime.UtcNow, DateTime.UtcNow);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, songB.SourceFilePath, "Song B", "Album B",
            ["Artist B"], ["Pop"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id,
            [new SongImportMetadata("/music/Song A.mp3", DateTime.UtcNow, DateTime.UtcNow), songB],
            cancellationToken: TestContext.Current.CancellationToken);

        // Nothing of the rolled back song should be saved along with the next one
        job.Exceptions.Count.ShouldBe(1);
        scenario.DbContext.Songs.Select(s => s.Title).ToList().ShouldBe(["Song B"]);
        scenario.DbContext.Albums.Select(a => a.Name).ToList().ShouldBe(["Album B"]);
        scenario.DbContext.Artists.Select(a => a.Name).ToList().ShouldBe(["Artist B"]);
        scenario.DbContext.Genres.Select(g => g.Name).ToList().ShouldBe(["Pop"]);
    }

    [Fact]
    public void Songs_SamePathForTheSameOwner_IsRejectedByTheDatabase()
    {
        var scenario = new Scenario();
        scenario.CreateSong("Song A", repositoryPath: RepositoryPath);

        // Two songs can never share a file
        Should.Throw<DbUpdateException>(() => scenario.CreateSong("Song B", repositoryPath: RepositoryPath));
    }

    [Fact]
    public async Task ImportMusic_SongSave_HappensBeforeTheFileIsWritten()
    {
        // Records whether the file already existed when the song (and so its path) was saved
        bool? fileExistedWhenSongWasSaved = null;
        Scenario? scenario = null;

        var interceptor = new FailingSaveChangesInterceptor(call =>
        {
            if (call == 2)
            {
                fileExistedWhenSongWasSaved = scenario!.FileSystem.File.Exists(RepositoryPath);
            }

            return null;
        });
        scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // The path should be claimed in the database first, and only then the file written
        job.Exceptions.ShouldBeEmpty();
        fileExistedWhenSongWasSaved.ShouldBe(false);
        RepositoryFiles(scenario).ShouldBe([RepositoryPath]);
    }

    [Fact]
    public async Task ImportMusic_CommitFails_RemovesTheWrittenFile()
    {
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();
        interceptor.Armed = true;

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // The file was written before the commit, so it should be removed along with the song
        job.Exceptions.Count.ShouldBe(1);
        scenario.DbContext.Songs.ShouldBeEmpty();
        RepositoryFiles(scenario).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Song")] // Same path: the file is rewritten in place
    [InlineData("Other Song")] // Another path: the file moves
    public async Task ImportMusic_ReImportCommitFails_KeepsTheOriginalFile(string newTitle)
    {
        // Setup: an imported song
        var interceptor = new FailingCommitInterceptor();
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);
        await musicService.ImportRepositorySongs(scenario.DbContext, CreateJob(), scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        var songId = scenario.DbContext.Songs.Single().Id;
        var originalContent = scenario.FileSystem.File.ReadAllBytes(RepositoryPath);

        // Its source changes content (and maybe title, and so its path), and the re-import then fails to commit
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, SongPath, newTitle, "My Album", ["My Artist"],
            ["Rock"]);
        interceptor.Armed = true;

        var job = CreateJob();
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id,
            [new SongImportMetadata(SongPath, DateTime.UtcNow, DateTime.UtcNow, songId)],
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Overwrite,
            cancellationToken: TestContext.Current.CancellationToken);

        // The song keeps its old path and checksum in the database, so its file should be back as it was
        job.Exceptions.Count.ShouldBe(1);
        RepositoryFiles(scenario).ShouldBe([RepositoryPath]);
        scenario.FileSystem.File.ReadAllBytes(RepositoryPath).ShouldBe(originalContent);
    }

    [Fact]
    public async Task ImportMusic_RetryAfterAnEarlierSongOfTheSameArtist_CreatesTheAlbumOnce()
    {
        // Song A creates "Shared Artist"; the first attempt of Song B creates "Album B" under that artist, then
        // deadlocks on its final save (calls: A = 1, 2; B = 3, 4 fails; B retry = 5, 6)
        var interceptor = new FailingSaveChangesInterceptor(call => call == 4 ? Deadlock() : null);
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        var songA = new SongImportMetadata("/music/Song A.mp3", DateTime.UtcNow, DateTime.UtcNow);
        MockMusicFile.Create(scenario.FileSystem, songA.SourceFilePath, "Song A", "Album A", ["Shared Artist"],
            ["Rock"]);

        var songB = new SongImportMetadata("/music/Song B.mp3", DateTime.UtcNow, DateTime.UtcNow);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, songB.SourceFilePath, "Song B", "Album B",
            ["Shared Artist"], ["Rock"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, [songA, songB],
            cancellationToken: TestContext.Current.CancellationToken);

        // The retry should not bring back the album of the rolled back attempt
        job.Exceptions.ShouldBeEmpty();
        interceptor.Calls.ShouldBe(6);
        scenario.DbContext.Albums.Select(a => a.Name).ToList().ShouldBe(["Album A", "Album B"], ignoreOrder: true);
        scenario.DbContext.Artists.Select(a => a.Name).ToList().ShouldBe(["Shared Artist"]);
        scenario.DbContext.Songs.Select(s => s.Title).ToList().ShouldBe(["Song A", "Song B"], ignoreOrder: true);
    }

    [Fact]
    public async Task ImportMusic_MergeHitsAConcurrencyConflict_IsRetried()
    {
        // The merge's save deadlocks on the first attempt
        var interceptor = new FailingSaveChangesInterceptor(call => call == 1 ? Deadlock() : null);
        var scenario = new Scenario(interceptor);
        var musicService = scenario.CreateMusicService(
            new SongMergeService(Substitute.For<ILogger<SongMergeService>>()));
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        // The library already has this file's content as one song, and the upload claims to be another song
        var (algorithm, checksum) = Checksum(scenario, SongPath);
        var keptSong = scenario.CreateSong("Kept", repositoryPath: RepositoryPath, checksum: checksum,
            checksumAlgorithm: algorithm);
        var mergedSong = scenario.CreateSong("Merged", repositoryPath: "/data/admin/Merged.mp3");

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id,
            [new SongImportMetadata(SongPath, DateTime.UtcNow, DateTime.UtcNow, mergedSong.Id)],
            cancellationToken: TestContext.Current.CancellationToken);

        // The retry should complete the merge into the song that has the content
        job.Exceptions.ShouldBeEmpty();
        job.SongMapping.Values.Select(s => s.Id).ShouldBe([keptSong.Id]);
        scenario.DbContext.Songs.Select(s => s.Id).ToList().ShouldBe([keptSong.Id]);
    }

    [Fact]
    public async Task ImportMusic_CallerHasUnsavedChanges_DoesNotSaveThem()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = CreateJob();

        MockMusicFile.Create(scenario.FileSystem, SongPath, "Song", "My Album", ["My Artist"], ["Rock"]);

        // The caller has a pending change it has not saved (yet)
        var artist = scenario.CreateArtist("Unsaved Rename");
        artist.Name = "Renamed";

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            cancellationToken: TestContext.Current.CancellationToken);

        // The import should only save its own song
        job.Exceptions.ShouldBeEmpty();
        await using var db = scenario.DbContextFactory.CreateDbContext();
        db.Artists.Where(a => a.Id == artist.Id).Select(a => a.Name).Single().ShouldBe("Unsaved Rename");
    }

    private static MusicImportJob CreateJob() => new(Substitute.For<ILogger<MusicImportJob>>());

    private static AdvisoryLockKey KeyFor(LockedResource resource, Scenario scenario)
    {
        var ownerId = scenario.AdminUser.Id;

        return resource switch
        {
            LockedResource.Album => AdvisoryLockKey.Create(AdvisoryLockScope.Album, ownerId, "My Artist", "My Album"),
            LockedResource.AlbumArtist => AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "My Artist"),
            LockedResource.SongArtist => AdvisoryLockKey.Create(AdvisoryLockScope.Artist, ownerId, "Guest Artist"),
            LockedResource.Checksum => ChecksumKey(scenario, SongPath),
            _ => throw new ArgumentOutOfRangeException(nameof(resource)),
        };
    }

    private static AdvisoryLockKey ChecksumKey(Scenario scenario, string filePath)
    {
        var algorithm = ChecksumService.CreateChecksumAlgorithm();
        var checksum = ChecksumService.CalculateChecksum(scenario.FileSystem, algorithm, filePath);

        return AdvisoryLockKey.Create(AdvisoryLockScope.SongChecksum, scenario.AdminUser.Id,
            algorithm.GetType().Name, checksum);
    }

    private static (string Algorithm, string Checksum) Checksum(Scenario scenario, string filePath)
    {
        var algorithm = ChecksumService.CreateChecksumAlgorithm();

        return (algorithm.GetType().Name, ChecksumService.CalculateChecksum(scenario.FileSystem, algorithm, filePath));
    }

    /// <summary>The files of the repository, outside of its <c>.temp</c> folder.</summary>
    private static List<string> RepositoryFiles(Scenario scenario) =>
        scenario.FileSystem.Directory.Exists("/data")
            ? scenario.FileSystem.Directory.GetFiles("/data", "*", SearchOption.AllDirectories)
                .Where(path => !path.StartsWith("/data/.temp/"))
                .ToList()
            : [];

    private static PostgresException Deadlock() =>
        new("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected);

    /// <summary>Fails the n-th <c>SaveChangesAsync</c> call (1-based) with the exception returned for it, if any.</summary>
    private sealed class FailingSaveChangesInterceptor(Func<int, Exception?> failureForCall) : SaveChangesInterceptor
    {
        public int Calls { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;

            if (failureForCall(Calls) is { } failure)
            {
                throw failure;
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
