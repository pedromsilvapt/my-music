using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory.Models;

namespace MyMusic.Common.Tests.Services.BackgroundJobs;

/// <summary>
/// Seeds the work items of the background jobs into a <see cref="Scenario"/>.
/// </summary>
internal static class BackgroundJobTestData
{
    public static Source CreateSource(this Scenario scenario, string name = "Test Source")
    {
        var source = new Source { Name = name, Icon = "icon", Address = "http://test.com", IsPaid = false };

        scenario.DbContext.Sources.Add(source);
        scenario.DbContext.SaveChanges();

        return source;
    }

    public static PurchasedSong CreatePurchase(
        this Scenario scenario,
        Source source,
        long userId,
        PurchasedSongStatus status,
        string title = "Song",
        string? errorMessage = null,
        DateTime? createdAt = null)
    {
        var purchase = new PurchasedSong
        {
            SourceId = source.Id,
            UserId = userId,
            ExternalId = $"ext-{title}",
            Title = title,
            SubTitle = "Artist • Album",
            Status = status,
            Progress = status == PurchasedSongStatus.Queued ? 0 : 100,
            ErrorMessage = errorMessage,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

        scenario.DbContext.PurchasedSongs.Add(purchase);
        scenario.DbContext.SaveChanges();

        return purchase;
    }

    public static MetadataFetchTask CreateMetadataFetchTask(
        this Scenario scenario,
        Song song,
        MetadataFetchStatus status,
        string? errorMessage = null,
        MetadataFetchFailureReason failureReason = MetadataFetchFailureReason.None,
        DateTime? completedAt = null)
    {
        var task = new MetadataFetchTask
        {
            SongId = song.Id,
            Status = status,
            Progress = status == MetadataFetchStatus.Queued ? 0 : 100,
            ErrorMessage = errorMessage,
            FailureReason = failureReason,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = completedAt,
        };

        scenario.DbContext.MetadataFetchTasks.Add(task);
        scenario.DbContext.SaveChanges();

        return task;
    }

    public static SongHistoryQueue CreateSongHistoryQueueEntry(
        this Scenario scenario,
        long songId,
        int revision,
        int errorCount = 0,
        string? lastError = null,
        DateTime? processedAt = null,
        DateTime? createdAt = null)
    {
        var now = DateTime.UtcNow;
        var entry = new SongHistoryQueue
        {
            SongId = songId,
            SongRevision = revision,
            TransactionId = 1000 + revision,
            Data = new SongSnapshot
            {
                Title = "Snapshot",
                Label = "Label",
                RepositoryPath = "/music/Snapshot.mp3",
                Checksum = "abc",
                ChecksumAlgorithm = "XxHash128",
                Action = "updated",
                AddedAt = now,
                CreatedAt = now,
                ModifiedAt = now,
                Artists = [],
                Genres = [],
                Sources = [],
                Devices = [],
            },
            CreatedAt = createdAt ?? now,
            ProcessedAt = processedAt,
            ErrorCount = errorCount,
            LastError = lastError,
        };

        scenario.DbContext.SongHistoryQueues.Add(entry);
        scenario.DbContext.SaveChanges();

        return entry;
    }

    public static WishlistItem CreateWishlistItem(
        this Scenario scenario,
        Source source,
        User owner,
        string query,
        WishlistItemStatus status = WishlistItemStatus.Active,
        int continuousFailedCount = 0,
        string? lastErrorMessage = null,
        DateTime? updatedAt = null)
    {
        var item = new WishlistItem
        {
            Owner = owner,
            SourceId = source.Id,
            Query = query,
            Hash = $"hash-{query}",
            Status = status,
            ContinuousFailedCount = continuousFailedCount,
            LastErrorMessage = lastErrorMessage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow,
        };

        scenario.DbContext.WishlistItems.Add(item);
        scenario.DbContext.SaveChanges();

        return item;
    }
}
