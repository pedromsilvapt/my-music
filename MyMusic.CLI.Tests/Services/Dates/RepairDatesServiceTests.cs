namespace MyMusic.CLI.Tests.Services.Dates;

using Microsoft.Extensions.Logging;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Commands;
using MyMusic.CLI.Services.Dates;
using MyMusic.CLI.Services.Sync;
using MyMusic.CLI.Services.Sync.Types;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

public class RepairDatesServiceTests
{
    private const long DeviceId = 7;
    private const string RepositoryPath = "/music";
    private const string SongPath = "Artist/song.mp3";

    private static readonly DateTime CreatedAt = new(2019, 7, 15, 8, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime ModifiedAt = new(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastSyncedAt = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ISyncConfig _config = Substitute.For<ISyncConfig>();
    private readonly IMyMusicClient _client = Substitute.For<IMyMusicClient>();
    private readonly IFileOps _fileOps = Substitute.For<IFileOps>();
    private readonly ILogger<RepairDatesService> _logger = Substitute.For<ILogger<RepairDatesService>>();

    public RepairDatesServiceTests()
    {
        _config.GetDeviceIdAsync(Arg.Any<CancellationToken>()).Returns(DeviceId);
        _config.GetRepositoryPath().Returns(RepositoryPath);
        _config.GetExcludePatterns().Returns([]);
        _config.GetFileModifiedAt().Returns(FileModifiedAtSource.ServerCreatedAt);
    }

    [Theory]
    [InlineData(FileModifiedAtSource.ServerCreatedAt)]
    [InlineData(FileModifiedAtSource.ServerModifiedAt)]
    public async Task Repair_FileDatedAtTheSync_GetsTheConfiguredServerDate(FileModifiedAtSource source)
    {
        _config.GetFileModifiedAt().Returns(source);
        SetupSongs(Song());
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        var expected = source == FileModifiedAtSource.ServerCreatedAt ? CreatedAt : ModifiedAt;
        result.ShouldBe(new RepairDatesResult { Redated = 1 });
        await _fileOps.Received(1).SetModificationTimeAsync(FullPath(SongPath), expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repair_SetToNow_FileGetsTheDateOfItsLastSync()
    {
        _config.GetFileModifiedAt().Returns(FileModifiedAtSource.Now);
        SetupSongs(Song());
        SetupFile(SongPath, CreatedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { Redated = 1 });
        await _fileOps.Received(1).SetModificationTimeAsync(FullPath(SongPath), LastSyncedAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repair_FileWithoutSong_IsSkippedAsNotSynced()
    {
        SetupSongs(Song() with { SongId = null });
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { NotSynced = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_FileMarkedForRemoval_IsSkippedAsNotSynced()
    {
        SetupSongs(Song() with { Action = "Remove" });
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { NotSynced = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_NeverSyncedFile_IsSkippedAsNotSynced()
    {
        // Also what an older server, which sends no dates, results in
        SetupSongs(new DeviceSongItem { SongId = 1, Path = SongPath, Action = "Download" });
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { NotSynced = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_ExcludedPath_IsSkipped()
    {
        _config.GetExcludePatterns().Returns(["Artist/"]);
        SetupSongs(Song());
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { Excluded = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_FileNotOnTheDevice_IsSkippedAsMissing()
    {
        SetupSongs(Song());

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { Missing = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_FileEditedOnTheDeviceAfterTheSync_IsSkipped()
    {
        // Re-dating it would hide the edit from the next sync
        SetupSongs(Song());
        SetupFile(SongPath, LastSyncedAt.AddTicks(10));

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { EditedLocally = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_FileDateWithinTheSyncPrecisionOfTheLastSync_IsNotTakenAsEdited()
    {
        SetupSongs(Song());
        SetupFile(SongPath, LastSyncedAt.AddTicks(9));

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { Redated = 1 });
    }

    [Fact]
    public async Task Repair_SongChangedOnTheServerAfterTheSync_IsSkipped()
    {
        // The next sync downloads it, and dates the file then
        _config.GetFileModifiedAt().Returns(FileModifiedAtSource.ServerModifiedAt);
        SetupSongs(Song() with { ServerModifiedAt = LastSyncedAt.AddTicks(10) });
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { ChangedOnServer = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_FileAlreadyHasTheDate_IsSkipped()
    {
        SetupSongs(Song());
        SetupFile(SongPath, CreatedAt.AddTicks(9));

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { UpToDate = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_DryRun_CountsTheFileWithoutChangingIt()
    {
        SetupSongs(Song());
        SetupFile(SongPath, LastSyncedAt);

        var result = await CreateService().RepairAsync(dryRun: true);

        result.ShouldBe(new RepairDatesResult { Redated = 1 });
        await ShouldNotChangeAnyFileAsync();
    }

    [Fact]
    public async Task Repair_DateCannotBeSet_CountsAFailureAndGoesOn()
    {
        const string otherPath = "Artist/other.mp3";
        SetupSongs(Song(), Song() with { SongId = 2, Path = otherPath });
        SetupFile(SongPath, LastSyncedAt);
        SetupFile(otherPath, LastSyncedAt);
        _fileOps.SetModificationTimeAsync(FullPath(SongPath), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("denied"));

        var result = await CreateService().RepairAsync(dryRun: false);

        result.ShouldBe(new RepairDatesResult { Redated = 1, Failed = 1 });
        await _fileOps.Received(1).SetModificationTimeAsync(FullPath(otherPath), CreatedAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildSummary_ListsOnlyTheSkipReasonsThatHappened()
    {
        var summary = RepairDatesCommand.BuildSummary(new RepairDatesResult { Redated = 3, EditedLocally = 2, Missing = 1 });

        summary.ShouldBe("3 re-dated, 3 skipped (2 edited locally, 1 missing)");
    }

    private RepairDatesService CreateService() => new(_config, _client, _fileOps, _logger);

    private static DeviceSongItem Song() => new()
    {
        SongId = 1,
        Path = SongPath,
        ServerCreatedAt = CreatedAt,
        ServerModifiedAt = ModifiedAt,
        LastSyncedModifiedAt = LastSyncedAt,
    };

    private static string FullPath(string relativePath) => Path.Combine(RepositoryPath, relativePath);

    private void SetupSongs(params DeviceSongItem[] songs) =>
        _client.GetDeviceSongsAsync(DeviceId, Arg.Any<CancellationToken>())
            .Returns(new GetDeviceSongsResponse { Songs = songs.ToList() });

    private void SetupFile(string relativePath, DateTime modifiedAt)
    {
        _fileOps.FileExists(FullPath(relativePath)).Returns(true);
        _fileOps.GetModificationTimeAsync(FullPath(relativePath), Arg.Any<CancellationToken>()).Returns(modifiedAt);
    }

    private Task ShouldNotChangeAnyFileAsync() =>
        _fileOps.DidNotReceive().SetModificationTimeAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
}
