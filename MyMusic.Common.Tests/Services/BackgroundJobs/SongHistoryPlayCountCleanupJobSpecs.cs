using Microsoft.Extensions.Logging;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using NSubstitute;
using Shouldly;
using SongHistoryEntity = MyMusic.Common.Entities.SongHistory;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

public class SongHistoryPlayCountCleanupJobSpecs
{
    private static readonly FieldChange<int> PlayCountChange = new() { Old = 1, New = 2 };

    private readonly Scenario _scenario = new();
    private readonly SongHistoryPlayCountCleanupJob _job = new();

    public static TheoryData<string, SongHistoryDelta, string> Revisions => new()
    {
        { "play count only", new SongHistoryDelta { Action = "updated", PlayCount = PlayCountChange }, "updated" },
        { "play count without action", new SongHistoryDelta { PlayCount = PlayCountChange }, "updated" },
        {
            "play count and title",
            new SongHistoryDelta
            {
                Action = "updated",
                PlayCount = PlayCountChange,
                Title = new FieldChange<string> { Old = "A", New = "B" },
            },
            "updated"
        },
        {
            "play count and artists",
            new SongHistoryDelta { PlayCount = PlayCountChange, Artists = new FieldChange<List<SongSnapshotArtist>>() },
            "updated"
        },
        { "title only", new SongHistoryDelta { Title = new FieldChange<string> { Old = "A", New = "B" } }, "updated" },
        { "empty", new SongHistoryDelta(), "updated" },
        {
            "created delta with play count",
            new SongHistoryDelta { Action = "created", PlayCount = PlayCountChange },
            "updated"
        },
        {
            "created revision with play count",
            new SongHistoryDelta { PlayCount = PlayCountChange },
            SongHistoryEntity.CreatedAction
        },
    };

    [Theory]
    [MemberData(nameof(Revisions))]
    public async Task GetCounters_Revision_CountsWhatTheCleanupRemoves(string _, SongHistoryDelta diff, string action)
    {
        // Arrange: a baseline keeps the song's history from being emptied by the cleanup (a song has only one)
        var song = _scenario.CreateSong("Song");
        if (action != SongHistoryEntity.CreatedAction)
        {
            _scenario.CreateSongHistoryRevision(song.Id, 1, new SongHistoryDelta(), SongHistoryEntity.CreatedAction);
        }

        _scenario.CreateSongHistoryRevision(song.Id, 2, diff, action);

        // Act: count the queued revisions, then let the cleanup remove them
        var counters = await _job.GetCountersAsync(_scenario.DbContext, _scenario.AdminUser.Id,
            CancellationToken.None);
        var cleanup = new SongHistoryPlayCountCleanupService(_scenario.DbContext,
            Substitute.For<ISongHistoryNotifier>(), Substitute.For<ILogger<SongHistoryPlayCountCleanupService>>());
        var (_, removed, _) = await cleanup.CleanupBatchAsync(0, 50, CancellationToken.None);

        // Assert: the SQL match agrees with the cleanup's own check, and nothing is left queued afterwards
        counters.Queued.ShouldBe(removed);
        counters.Processed.ShouldBeNull();
        counters.Failed.ShouldBeNull();
        var after = await _job.GetCountersAsync(_scenario.DbContext, _scenario.AdminUser.Id, CancellationToken.None);
        after.Queued.ShouldBe(0);
    }

    [Fact]
    public async Task GetCounters_PlayCountOnlyRevision_IsCounted()
    {
        // Arrange
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongHistoryRevision(song.Id, 1, new SongHistoryDelta { PlayCount = PlayCountChange });

        // Act
        var counters = await _job.GetCountersAsync(_scenario.DbContext, _scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert: guards the parity theory against both sides matching nothing
        counters.Queued.ShouldBe(1);
    }

    [Fact]
    public async Task GetCounters_SeveralRevisionsAndUsers_CountsTheUsersPlayCountOnlyRevisions()
    {
        // Arrange
        var song = _scenario.CreateSong("Song");
        _scenario.CreateSongHistoryRevision(song.Id, 1, new SongHistoryDelta(), SongHistoryEntity.CreatedAction);
        _scenario.CreateSongHistoryRevision(song.Id, 2, new SongHistoryDelta { PlayCount = PlayCountChange });
        _scenario.CreateSongHistoryRevision(song.Id, 3, new SongHistoryDelta { PlayCount = PlayCountChange });
        var other = _scenario.CreateUser("Other", "other");
        var otherSong = _scenario.CreateSong("Other Song", ownerId: other.Id);
        _scenario.CreateSongHistoryRevision(otherSong.Id, 1, new SongHistoryDelta { PlayCount = PlayCountChange });

        // Act
        var counters = await _job.GetCountersAsync(_scenario.DbContext, _scenario.AdminUser.Id,
            CancellationToken.None);

        // Assert
        counters.Queued.ShouldBe(2);
    }

    [Fact]
    public async Task GetFailures_ReturnsEmptyPage()
    {
        // Act
        var failures = await _job.GetFailuresAsync(_scenario.DbContext, _scenario.AdminUser.Id, 1, 20,
            CancellationToken.None);

        // Assert
        failures.Total.ShouldBe(0);
        failures.Items.ShouldBeEmpty();
    }
}
