using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using MyMusic.Common.Services.Songs;
using Shouldly;

namespace MyMusic.Common.Tests.Services.Songs;

public class SongHistoryPendingServiceSpecs
{
    internal static SongHistoryQueue AddQueueEntry(
        Scenario scenario,
        long songId,
        int errorCount = 0,
        DateTime? processedAt = null)
    {
        var entry = new SongHistoryQueue
        {
            SongId = songId,
            // Revisions are unique per song
            SongRevision = scenario.DbContext.SongHistoryQueues.Count(q => q.SongId == songId) + 1,
            Data = new SongSnapshot
            {
                Title = "Snapshot",
                Label = "Snapshot",
                RepositoryPath = "/music/snapshot.mp3",
                Checksum = "abc",
                ChecksumAlgorithm = "XxHash128",
                Action = "updated",
            },
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = processedAt,
            ErrorCount = errorCount,
        };
        scenario.DbContext.SongHistoryQueues.Add(entry);
        scenario.DbContext.SaveChanges();
        return entry;
    }

    [Fact]
    public async Task HasPendingAsync_NoQueueEntries_ReturnsFalse()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var service = new SongHistoryPendingService(scenario.DbContext);

        (await service.HasPendingAsync(song.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task HasPendingAsync_UnprocessedEntry_ReturnsTrue()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        AddQueueEntry(scenario, song.Id, errorCount: SongHistoryWorker.MaxErrorCount - 1);
        var service = new SongHistoryPendingService(scenario.DbContext);

        (await service.HasPendingAsync(song.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasPendingAsync_OnlyProcessedOrDeadLetteredEntries_ReturnsFalse()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        AddQueueEntry(scenario, song.Id, processedAt: DateTime.UtcNow);
        AddQueueEntry(scenario, song.Id, errorCount: SongHistoryWorker.MaxErrorCount);
        var service = new SongHistoryPendingService(scenario.DbContext);

        (await service.HasPendingAsync(song.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task HasPendingAsync_EntryForOtherSong_ReturnsFalse()
    {
        var scenario = new Scenario();
        var song = scenario.CreateSong("Song");
        var other = scenario.CreateSong("Other");
        AddQueueEntry(scenario, other.Id);
        var service = new SongHistoryPendingService(scenario.DbContext);

        (await service.HasPendingAsync(song.Id)).ShouldBeFalse();
    }
}
