using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SongChecksumHistorySpecs
{
    #region Lookup

    [Fact]
    public async Task FindSongsByChecksums_CurrentChecksum_ReturnsSong()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song", checksum: "current");
        var repo = new UserMusicService(scenario.DbContext, scenario.AdminUser.Id);

        // Act
        var songs = await repo.FindSongsByChecksums(["current"], song.ChecksumAlgorithm);

        // Assert
        songs["current"].Id.ShouldBe(song.Id);
    }

    [Fact]
    public async Task FindSongsByChecksums_PreviousChecksum_ReturnsSong()
    {
        // Arrange: the song's file used to have another checksum
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song", checksum: "current");
        scenario.AddChecksumHistory(song, "old");
        var repo = new UserMusicService(scenario.DbContext, scenario.AdminUser.Id);

        // Act
        var songs = await repo.FindSongsByChecksums(["old"], song.ChecksumAlgorithm);

        // Assert
        songs["old"].Id.ShouldBe(song.Id);
    }

    [Fact]
    public async Task FindSongsByChecksums_CurrentAndPreviousMatches_PrefersCurrent()
    {
        // Arrange: one song had the checksum in the past, another has it now
        var scenario = new Scenario();
        var previousOwner = scenario.CreateSong("Previous", checksum: "newer");
        scenario.AddChecksumHistory(previousOwner, "shared");
        var currentOwner = scenario.CreateSong("Current", checksum: "shared");
        var repo = new UserMusicService(scenario.DbContext, scenario.AdminUser.Id);

        // Act
        var songs = await repo.FindSongsByChecksums(["shared"], currentOwner.ChecksumAlgorithm);

        // Assert
        songs["shared"].Id.ShouldBe(currentOwner.Id);
    }

    [Fact]
    public async Task FindSongsByChecksums_OtherUsersHistory_IsIgnored()
    {
        // Arrange
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Song", ownerId: otherUser.Id, checksum: "current");
        scenario.AddChecksumHistory(song, "old");
        var repo = new UserMusicService(scenario.DbContext, scenario.AdminUser.Id);

        // Act
        var songs = await repo.FindSongsByChecksums(["old", "current"], song.ChecksumAlgorithm);

        // Assert
        songs.ShouldBeEmpty();
    }

    #endregion

    #region Merge

    [Fact]
    public async Task MergeSongs_UnionsChecksumHistoriesWithoutDuplicates()
    {
        // Arrange: both songs share one previous checksum, and each has its own
        var scenario = new Scenario();
        var keep = scenario.CreateSong("Keep", checksum: "keep-current");
        var mergeFrom = scenario.CreateSong("MergeFrom", checksum: "from-current");
        scenario.AddChecksumHistory(keep, "keep-old", "shared-old");
        scenario.AddChecksumHistory(mergeFrom, "from-current", "shared-old");
        var service = new SongMergeService(Substitute.For<ILogger<SongMergeService>>());

        // Act
        var result = await service.MergeSongsAsync(scenario.DbContext, keep.Id, mergeFrom.Id);

        // Assert: the kept song has every previous checksum once (the merged song's current one included),
        // and the merged song's rows are gone
        result.Success.ShouldBeTrue();
        var checksums = await scenario.DbContext.SongChecksums.AsNoTracking().ToListAsync();
        checksums.ShouldAllBe(sc => sc.SongId == keep.Id);
        checksums.Select(sc => sc.Checksum)
            .ShouldBe(["keep-old", "shared-old", "from-current"], ignoreOrder: true);
    }

    [Fact]
    public async Task MergeSongs_MergedSongWithoutHistory_KeepsItsCurrentChecksum()
    {
        // Arrange: the merged song has no history rows (e.g. created before the trigger existed)
        var scenario = new Scenario();
        var keep = scenario.CreateSong("Keep", checksum: "keep-current");
        var mergeFrom = scenario.CreateSong("MergeFrom", checksum: "from-current");
        var service = new SongMergeService(Substitute.For<ILogger<SongMergeService>>());

        // Act
        await service.MergeSongsAsync(scenario.DbContext, keep.Id, mergeFrom.Id);

        // Assert
        var checksums = await scenario.DbContext.SongChecksums.AsNoTracking().ToListAsync();
        checksums.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            sc => sc.SongId.ShouldBe(keep.Id),
            sc => sc.Checksum.ShouldBe("from-current"));
    }

    [Fact]
    public async Task MergeSongs_MergedHistoryHoldsKeptSongsCurrentChecksum_IsNotAddedToHistory()
    {
        // Arrange: the merged song once had the kept song's current content
        var scenario = new Scenario();
        var keep = scenario.CreateSong("Keep", checksum: "keep-current");
        var mergeFrom = scenario.CreateSong("MergeFrom", checksum: "from-current");
        scenario.AddChecksumHistory(mergeFrom, "keep-current");
        var service = new SongMergeService(Substitute.For<ILogger<SongMergeService>>());

        // Act
        await service.MergeSongsAsync(scenario.DbContext, keep.Id, mergeFrom.Id);

        // Assert: the history only holds previous checksums, never the current one
        var checksums = await scenario.DbContext.SongChecksums.AsNoTracking().ToListAsync();
        checksums.Select(sc => sc.Checksum).ShouldBe(["from-current"]);
    }

    [Fact]
    public async Task MergeSongs_RecordsTheMerge()
    {
        // Arrange
        var scenario = new Scenario();
        var keep = scenario.CreateSong("Keep", checksum: "keep-current");
        var mergeFrom = scenario.CreateSong("MergeFrom", checksum: "from-current");
        var service = new SongMergeService(Substitute.For<ILogger<SongMergeService>>());

        // Act
        await service.MergeSongsAsync(scenario.DbContext, keep.Id, mergeFrom.Id);

        // Assert
        var merges = await scenario.DbContext.SongMerges.AsNoTracking().ToListAsync();
        merges.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            m => m.KeptSongId.ShouldBe(keep.Id),
            m => m.MergedSongId.ShouldBe(mergeFrom.Id),
            m => m.OwnerId.ShouldBe(scenario.AdminUser.Id),
            m => m.Kind.ShouldBe(SongMergeKind.ImportDuplicate));
    }

    [Fact]
    public async Task MergeSongs_KeptSongMergedLater_LeavesItsOwnMergesUntouched()
    {
        // Arrange: A was merged into B
        var scenario = new Scenario();
        var a = scenario.CreateSong("A", checksum: "a");
        var b = scenario.CreateSong("B", checksum: "b");
        var c = scenario.CreateSong("C", checksum: "c");
        var service = new SongMergeService(Substitute.For<ILogger<SongMergeService>>());
        await service.MergeSongsAsync(scenario.DbContext, b.Id, a.Id);

        // Act: B is merged into C
        await service.MergeSongsAsync(scenario.DbContext, c.Id, b.Id);

        // Assert: the merges form a tree (A -> B -> C); A's merge is neither removed nor re-pointed at C
        var merges = await scenario.DbContext.SongMerges.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        merges.Select(m => (m.KeptSongId, m.MergedSongId)).ShouldBe([(b.Id, a.Id), (c.Id, b.Id)]);
    }

    #endregion

    #region Import

    /// <summary>
    /// Imports a file, then pretends the song's file has since changed to newer content: the imported file is now a
    /// previous version of the song. Returns the song and the file's (old) checksum.
    /// </summary>
    private static async Task<(Song Song, string OldChecksum)> ArrangeSongWithNewerVersion(Scenario scenario, IMusicService musicService)
    {
        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");
        job.Exceptions.ShouldBeEmpty();

        var song = scenario.DbContext.Songs.Single();
        var oldChecksum = song.Checksum;
        song.Checksum = "newer-checksum";
        scenario.DbContext.SaveChanges();
        scenario.AddChecksumHistory(song, oldChecksum);

        return (song, oldChecksum);
    }

    private static async Task<MusicImportJob> ImportAgain(Scenario scenario, IMusicService musicService,
        DuplicateSongsHandlingStrategy strategy)
    {
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: strategy);
        job.Exceptions.ShouldBeEmpty();
        return job;
    }

    [Fact]
    public async Task Import_FileOfPreviousVersion_Skip_IsSkippedWithoutTouchingSong()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        await ArrangeSongWithNewerVersion(scenario, musicService);

        // Act: import the old file again
        var job = await ImportAgain(scenario, musicService, DuplicateSongsHandlingStrategy.Skip);

        // Assert: skipped as a previous version, and the song keeps its newer content
        job.SkipReasons.ShouldHaveSingleItem().ShouldBeOfType<PreviousVersionChecksumSkipReason>();
        var songs = await scenario.DbContext.Songs.AsNoTracking().ToListAsync();
        songs.ShouldHaveSingleItem().Checksum.ShouldBe("newer-checksum");
    }

    [Fact]
    public async Task Import_FileOfPreviousVersion_Overwrite_RestoresThatVersion()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var (song, oldChecksum) = await ArrangeSongWithNewerVersion(scenario, musicService);

        // Act: import the old file again
        var job = await ImportAgain(scenario, musicService, DuplicateSongsHandlingStrategy.Overwrite);

        // Assert: the same song now has the old file's content again (in PostgreSQL, the trigger then swaps
        // the checksums in the history)
        job.SkipReasons.ShouldBeEmpty();
        var songs = await scenario.DbContext.Songs.AsNoTracking().ToListAsync();
        songs.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            s => s.Id.ShouldBe(song.Id),
            s => s.Checksum.ShouldBe(oldChecksum));
    }

    [Fact]
    public async Task Import_FileOfPreviousVersion_SplitWhenSuperseded_ImportsSeparateSong()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var (song, oldChecksum) = await ArrangeSongWithNewerVersion(scenario, musicService);

        // Act: import the old file again
        var job = await ImportAgain(scenario, musicService, DuplicateSongsHandlingStrategy.SplitWhenSuperseded);

        // Assert: a new song holds the old content, and the existing song keeps its newer content
        job.SkipReasons.ShouldBeEmpty();
        var songs = await scenario.DbContext.Songs.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
        songs.Select(s => (s.Id == song.Id, s.Checksum)).ShouldBe([(true, "newer-checksum"), (false, oldChecksum)]);
    }

    [Fact]
    public async Task Import_FileOfCurrentVersion_SplitWhenSuperseded_IsSkipped()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        await ImportAgain(scenario, musicService, DuplicateSongsHandlingStrategy.Skip);

        // Act: import the same file again
        var job = await ImportAgain(scenario, musicService, DuplicateSongsHandlingStrategy.SplitWhenSuperseded);

        // Assert: skipped as a duplicate of the song's current content
        job.SkipReasons.ShouldHaveSingleItem().ShouldBeOfType<DuplicateChecksumSkipReason>();
        (await scenario.DbContext.Songs.CountAsync()).ShouldBe(1);
    }

    #endregion

    #region IsPreviousVersion

    [Fact]
    public async Task IsPreviousVersion_SameChecksumOfAnotherAlgorithm_LooksUpTheHistory()
    {
        // Arrange: the checksum text equals the current one, but was computed with another algorithm
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song", checksum: "same");
        scenario.DbContext.SongChecksums.Add(new SongChecksum
        {
            SongId = song.Id,
            Checksum = "same",
            ChecksumAlgorithm = "OtherAlgorithm",
            CreatedAt = DateTime.UtcNow,
        });
        scenario.DbContext.SaveChanges();

        // Act & Assert
        (await SongChecksumHistory.IsPreviousVersionAsync(scenario.DbContext, song, "same", "OtherAlgorithm")).ShouldBeTrue();
        (await SongChecksumHistory.IsPreviousVersionAsync(scenario.DbContext, song, "same", song.ChecksumAlgorithm)).ShouldBeFalse();
    }

    #endregion
}
