using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Services.SongHistory;
using MyMusic.Common.Services.SongHistory.Models;
using NSubstitute;
using NSubstitute.Core;
using Shouldly;

namespace MyMusic.Common.Tests.Services.SongHistory;

public class SongHistoryWorkerSpecs
{
    private readonly ILogger<SongHistoryWorker> _logger = Substitute.For<ILogger<SongHistoryWorker>>();
    private readonly ISongHistoryDiffService _diffService = new SongHistoryDiffService();

    private static (SongHistoryWorker worker, Scenario scenario, ISongHistoryThumbnailService thumbnail, ISongHistorySnapshotService snapshot) CreateWorker(
        ISongHistoryThumbnailService? thumbnail = null,
        ISongHistorySnapshotService? snapshot = null,
        Config? config = null,
        ISongHistoryNotifier? notifier = null,
        IServiceScopeFactory? scopeFactory = null,
        Scenario? scenario = null)
    {
        scenario ??= new Scenario();
        var thumbnailService = thumbnail ?? new SongHistoryThumbnailService(
            Substitute.For<ILogger<SongHistoryThumbnailService>>());
        var snapshotService = snapshot ?? Substitute.For<ISongHistorySnapshotService>();
        var cfg = config ?? new Config
        {
            MusicRepositoryPath = "/data",
            SongHistoryWorkerEnabled = true,
            SongHistoryWorkerIntervalSeconds = 10,
            SongHistoryWorkerBatchSize = 50,
        };
        var worker = new SongHistoryWorker(
            scopeFactory ?? Substitute.For<IServiceScopeFactory>(),
            Options.Create(cfg),
            notifier ?? Substitute.For<ISongHistoryNotifier>(),
            Substitute.For<ILogger<SongHistoryWorker>>());
        return (worker, scenario, thumbnailService, snapshotService);
    }

    private static SongSnapshot BuildSnapshot(
        string title = "Song",
        string label = "Label",
        long songId = 0,
        string action = "updated",
        SongSnapshotCover? cover = null,
        List<SongSnapshotArtist>? artists = null,
        SongSnapshotAlbum? album = null)
    {
        var now = DateTime.UtcNow;
        return new SongSnapshot
        {
            Title = title,
            Label = label,
            AlbumId = album?.Id ?? 1,
            CoverId = cover?.Id,
            Year = 2024,
            Lyrics = null,
            Explicit = false,
            Size = 0,
            Track = null,
            Duration = TimeSpan.Zero,
            Bitrate = null,
            OwnerId = 1,
            Rating = null,
            IsFavorite = false,
            PlayCount = 0,
            RepositoryPath = $"/music/{title}.mp3",
            Checksum = "abc",
            ChecksumAlgorithm = "XxHash128",
            AddedAt = now,
            CreatedAt = now,
            ModifiedAt = now,
            FileModifiedAt = null,
            Action = action,
            Album = album,
            Artists = artists ?? [],
            Genres = [],
            Sources = [],
            Devices = [],
            Cover = cover,
        };
    }

    private static (string base64, string mimeType) MakeCoverImage(int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(40, 80, 160));
        }
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Jpeg);
        return (Convert.ToBase64String(stream.ToArray()), "image/jpeg");
    }

    private static SongHistoryQueue InsertQueueEntry(
        Scenario scenario,
        long songId,
        int revision,
        SongSnapshot data,
        long? transactionId = null,
        long? ownerId = null)
    {
        var entry = new SongHistoryQueue
        {
            SongId = songId,
            OwnerId = ownerId ?? scenario.SongOwnerId(songId),
            SongRevision = revision,
            TransactionId = transactionId,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null,
            ErrorCount = 0,
            LastError = null,
        };
        scenario.DbContext.SongHistoryQueues.Add(entry);
        scenario.DbContext.SaveChanges();
        return entry;
    }

    /// <summary>
    /// A scope factory whose scopes resolve everything a worker cycle asks for: the scenario's database and the given
    /// services, with the baseline backfill and the play count cleanup reporting nothing left to do.
    /// </summary>
    private static IServiceScopeFactory CreateScopeFactory(
        Scenario scenario,
        ISongHistoryDiffService diffService,
        ISongHistoryThumbnailService thumbnailService,
        ISongHistorySnapshotService snapshotService)
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(MusicDbContext)).Returns(scenario.DbContext);
        serviceProvider.GetService(typeof(ISongHistoryDiffService)).Returns(diffService);
        serviceProvider.GetService(typeof(ISongHistoryThumbnailService)).Returns(thumbnailService);
        serviceProvider.GetService(typeof(ISongHistorySnapshotService)).Returns(snapshotService);
        serviceProvider.GetService(typeof(ISongHistoryBaselineBackfillService))
            .Returns(Substitute.For<ISongHistoryBaselineBackfillService>());
        serviceProvider.GetService(typeof(ISongHistoryPlayCountCleanupService))
            .Returns(Substitute.For<ISongHistoryPlayCountCleanupService>());

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        return scopeFactory;
    }

    [Fact]
    public async Task ProcessQueue_EntryWithScalarChange_WritesDeltaToHistory()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var oldSnapshot = BuildSnapshot(title: "Old Title", songId: 1, action: "updated");
        InsertQueueEntry(scenario, songId: 1, revision: 1, oldSnapshot);
        var liveState = BuildSnapshot(title: "New Title", songId: 1, action: "updated");
        snapshot.GetCurrentSnapshotAsync(1, true, Arg.Any<CancellationToken>()).Returns(liveState);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(1);
        history.SongRevision.ShouldBe(1);
        history.DiffFormat.ShouldBe("delta");

        var delta = history.Diff;
        delta.Action.ShouldBe("updated");
        delta.Title.ShouldNotBeNull();
        delta.Title.Old.ShouldBe("Old Title");
        delta.Title.New.ShouldBe("New Title");
        delta.Label.ShouldBeNull();
        delta.Cover.ShouldBeNull();

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_HistoryKeepsTheOwnerOfTheQueueEntry()
    {
        // The song was deleted, so its queue entry is the only record left of who owned it
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var owner = scenario.CreateUser("Owner", "owner");
        var deletedSnapshot = BuildSnapshot(title: "Deleted Song", songId: 1, action: "deleted");
        InsertQueueEntry(scenario, songId: 1, revision: 1, deletedSnapshot, ownerId: owner.Id);
        snapshot.GetCurrentSnapshotAsync(1, true, Arg.Any<CancellationToken>()).Returns((SongSnapshot?)null);

        await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        var history = scenario.DbContext.SongHistories.Single();
        history.Action.ShouldBe("deleted");
        history.OwnerId.ShouldBe(owner.Id);
    }

    [Fact]
    public async Task ProcessQueue_EntryWithCoverChange_GeneratesThumbnailInDelta()
    {
        var (base64, mimeType) = MakeCoverImage(400, 400);
        // Covers are immutable in the system: a cover data change always creates a
        // new Artwork row with a new Id, so the old and new covers have distinct Ids.
        var oldCover = new SongSnapshotCover { Id = 1, MimeType = mimeType, Width = 400, Height = 400, Data = "old" };
        var newCover = new SongSnapshotCover { Id = 2, MimeType = mimeType, Width = 400, Height = 400, Data = base64 };
        var oldSnapshot = BuildSnapshot(title: "Song", songId: 2, action: "updated", cover: oldCover);
        var newSnapshot = BuildSnapshot(title: "Song", songId: 2, action: "updated", cover: newCover);
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        InsertQueueEntry(scenario, songId: 2, revision: 1, oldSnapshot);
        snapshot.GetCurrentSnapshotAsync(2, true, Arg.Any<CancellationToken>()).Returns(newSnapshot);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        var delta = history.Diff;
        delta.Cover.ShouldNotBeNull();
        delta.Cover.Old.ShouldNotBeNull();
        delta.Cover.New.ShouldNotBeNull();
        delta.Cover.New!.MimeType.ShouldBe(mimeType);
        delta.Cover.New.Width.ShouldBe(400);
        delta.Cover.New.Height.ShouldBe(400);

        var storedData = delta.Cover.New.Data;
        storedData.ShouldNotBeNull();
        storedData.ShouldNotBe(base64);

        var thumbBytes = Convert.FromBase64String(storedData!);
        using var ms = new MemoryStream(thumbBytes);
        using var img = Image.FromStream(ms);
        img.Width.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);
        img.Height.ShouldBeLessThanOrEqualTo(SongHistoryThumbnailService.MaxThumbnailSize);

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_EntryWithCoverChange_ThumbnailFails_KeepsCoverWithEmptyData()
    {
        var failingThumbnail = Substitute.For<ISongHistoryThumbnailService>();
        failingThumbnail.GenerateThumbnail(Arg.Any<string>(), Arg.Any<string>())
            .Returns((byte[]?)null);
        var (worker, scenario, _, snapshot) = CreateWorker(failingThumbnail);

        // Covers are immutable: a cover data change always creates a new Artwork
        // row with a new Id, so old and new covers have distinct Ids.
        var oldCover = new SongSnapshotCover { Id = 1, MimeType = "image/jpeg", Width = 400, Height = 400, Data = "AAAA" };
        var newCover = new SongSnapshotCover { Id = 2, MimeType = "image/jpeg", Width = 400, Height = 400, Data = "BBBB" };
        var oldSnapshot = BuildSnapshot(title: "Song", songId: 3, action: "updated", cover: oldCover);
        var newSnapshot = BuildSnapshot(title: "Song", songId: 3, action: "updated", cover: newCover);
        InsertQueueEntry(scenario, songId: 3, revision: 1, oldSnapshot);
        snapshot.GetCurrentSnapshotAsync(3, true, Arg.Any<CancellationToken>()).Returns(newSnapshot);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, failingThumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        var delta = history.Diff;
        delta.Cover.ShouldNotBeNull();
        delta.Cover.New.ShouldNotBeNull();
        delta.Cover.New!.MimeType.ShouldBe("image/jpeg");
        delta.Cover.New.Width.ShouldBe(400);
        delta.Cover.New.Data.ShouldBe(string.Empty);

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_EntryWithCoverUnchanged_NoCoverFieldChange()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var oldSnapshot = BuildSnapshot(title: "Song", songId: 4, action: "updated");
        var newSnapshot = BuildSnapshot(title: "Song", songId: 4, action: "updated");
        InsertQueueEntry(scenario, songId: 4, revision: 1, oldSnapshot);
        snapshot.GetCurrentSnapshotAsync(4, true, Arg.Any<CancellationToken>()).Returns(newSnapshot);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        var delta = history.Diff;
        delta.Action.ShouldBe("updated");
        delta.Cover.ShouldBeNull();
        delta.Title.ShouldBeNull();

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_DeletedEntry_LiveStateNull_WritesDeletedActionDelta()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var artists = new List<SongSnapshotArtist> { new() { Id = 1, Name = "A" } };
        var album = new SongSnapshotAlbum { Id = 1, Title = "Album" };
        var deletedSnapshot = BuildSnapshot(title: "Removed Song", songId: 5, action: "deleted", artists: artists, album: album);
        InsertQueueEntry(scenario, songId: 5, revision: 1, deletedSnapshot);
        snapshot.GetCurrentSnapshotAsync(5, true, Arg.Any<CancellationToken>()).Returns((SongSnapshot?)null);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        var delta = history.Diff;
        delta.Action.ShouldBe("deleted");
        delta.Title.ShouldBeNull();
        delta.Artists.ShouldBeNull();
        delta.Album.ShouldBeNull();
        delta.Cover.ShouldBeNull();

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_MultipleEntriesForSameSong_ProcessedAsSingleSong()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var liveState = BuildSnapshot(title: "V3", songId: 10, action: "updated");
        InsertQueueEntry(scenario, songId: 10, revision: 1, BuildSnapshot(title: "V1", songId: 10, action: "created"));
        InsertQueueEntry(scenario, songId: 10, revision: 2, BuildSnapshot(title: "V2", songId: 10, action: "updated"));
        InsertQueueEntry(scenario, songId: 10, revision: 3, BuildSnapshot(title: "V3", songId: 10, action: "updated"));
        snapshot.GetCurrentSnapshotAsync(10, true, Arg.Any<CancellationToken>()).Returns(liveState);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var histories = scenario.DbContext.SongHistories.OrderBy(h => h.SongRevision).ToList();
        histories.Count.ShouldBe(3);
        histories[0].SongRevision.ShouldBe(1);
        histories[0].Action.ShouldBe("created");
        histories[0].Diff.Action.ShouldBe("created");
        histories[0].Diff.Title.ShouldNotBeNull();
        histories[0].Diff.Title.Old.ShouldBeNull();
        histories[0].Diff.Title.New.ShouldBe("V2");
        histories[1].SongRevision.ShouldBe(2);
        histories[1].Diff.Action.ShouldBe("updated");
        histories[1].Diff.Title.ShouldNotBeNull();
        histories[1].Diff.Title.Old.ShouldBe("V2");
        histories[1].Diff.Title.New.ShouldBe("V3");
        histories[2].SongRevision.ShouldBe(3);
        histories[2].Diff.Action.ShouldBe("updated");
        histories[2].Diff.Title.ShouldBeNull();

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_NotifiesProcessedAndFailedSongs()
    {
        var notifier = Substitute.For<ISongHistoryNotifier>();
        var (worker, scenario, thumbnail, snapshot) = CreateWorker(notifier: notifier);

        InsertQueueEntry(scenario, songId: 30, revision: 1, BuildSnapshot(title: "Broken", songId: 30));
        snapshot.GetCurrentSnapshotAsync(30, true, Arg.Any<CancellationToken>())
            .Returns((Func<CallInfo, SongSnapshot>)(_ => throw new InvalidOperationException("boom")));

        InsertQueueEntry(scenario, songId: 31, revision: 1, BuildSnapshot(title: "Old", songId: 31));
        snapshot.GetCurrentSnapshotAsync(31, true, Arg.Any<CancellationToken>())
            .Returns(BuildSnapshot(title: "New", songId: 31));

        await worker.ProcessQueueAsync(scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        notifier.Received(1).Publish(30, SongHistoryNotificationKind.Failed);
        notifier.Received(1).Publish(31, SongHistoryNotificationKind.Processed);
        notifier.DidNotReceive().Publish(30, SongHistoryNotificationKind.Processed);
        notifier.DidNotReceive().Publish(31, SongHistoryNotificationKind.Failed);
    }

    [Fact]
    public async Task ProcessQueue_ProcessEntryThrows_IncrementsErrorCountAndContinues()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var badSnapshot = BuildSnapshot(title: "No Live State", songId: 20, action: "updated");
        var badEntry = InsertQueueEntry(scenario, songId: 20, revision: 1, badSnapshot);
        snapshot.GetCurrentSnapshotAsync(20, true, Arg.Any<CancellationToken>())
            .Returns((Func<CallInfo, SongSnapshot>)(_ => throw new InvalidOperationException("DB unavailable for song 20")));

        var goodSnapshot = BuildSnapshot(title: "Good Old", songId: 21, action: "updated");
        var goodLive = BuildSnapshot(title: "Good New", songId: 21, action: "updated");
        InsertQueueEntry(scenario, songId: 21, revision: 1, goodSnapshot);
        snapshot.GetCurrentSnapshotAsync(21, true, Arg.Any<CancellationToken>()).Returns(goodLive);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(1);

        var survivingQueue = scenario.DbContext.SongHistoryQueues.Single();
        survivingQueue.Id.ShouldBe(badEntry.Id);
        survivingQueue.ErrorCount.ShouldBe(1);
        survivingQueue.LastError.ShouldNotBeNull();
        survivingQueue.LastErrorAt.ShouldNotBeNull().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        survivingQueue.ProcessedAt.ShouldBeNull();

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(21);
        history.Diff.Title.ShouldNotBeNull();
        history.Diff.Title.Old.ShouldBe("Good Old");
        history.Diff.Title.New.ShouldBe("Good New");
    }

    [Fact]
    public async Task ProcessQueue_SongFailedWithinInterval_IsSkipped()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var failedEntry = InsertQueueEntry(scenario, songId: 80, revision: 1, BuildSnapshot(title: "Failed", songId: 80));
        failedEntry.ErrorCount = 1;
        failedEntry.LastError = "previous failure";
        failedEntry.LastErrorAt = DateTime.UtcNow;
        scenario.DbContext.SaveChanges();
        // Queued after the failure: a song's entries are processed together, so it waits for the retry too
        InsertQueueEntry(scenario, songId: 80, revision: 2, BuildSnapshot(title: "Failed Again", songId: 80));

        InsertQueueEntry(scenario, songId: 81, revision: 1, BuildSnapshot(title: "Good Old", songId: 81));
        snapshot.GetCurrentSnapshotAsync(81, true, Arg.Any<CancellationToken>())
            .Returns(BuildSnapshot(title: "Good New", songId: 81));

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        await snapshot.DidNotReceive().GetCurrentSnapshotAsync(80, Arg.Any<bool>(), Arg.Any<CancellationToken>());

        var surviving = scenario.DbContext.SongHistoryQueues.OrderBy(q => q.SongRevision).ToList();
        surviving.Count.ShouldBe(2);
        surviving.ShouldAllBe(q => q.SongId == 80);
        surviving[0].ErrorCount.ShouldBe(1);
        surviving[1].ErrorCount.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(81);
    }

    [Fact]
    public async Task ProcessQueue_SongFailedBeforeInterval_IsRetried()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var failedEntry = InsertQueueEntry(scenario, songId: 82, revision: 1, BuildSnapshot(title: "Old", songId: 82));
        failedEntry.ErrorCount = 1;
        failedEntry.LastError = "previous failure";
        failedEntry.LastErrorAt = DateTime.UtcNow.AddSeconds(-11);
        scenario.DbContext.SaveChanges();
        snapshot.GetCurrentSnapshotAsync(82, true, Arg.Any<CancellationToken>())
            .Returns(BuildSnapshot(title: "New", songId: 82));

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
        scenario.DbContext.SongHistories.Single().SongId.ShouldBe(82);
    }

    [Fact]
    public async Task Execute_MoreSongsThanBatchSize_ProcessesNextBatchWithoutWaiting()
    {
        const int songCount = 5;
        var scenario = new Scenario();
        var snapshot = Substitute.For<ISongHistorySnapshotService>();
        var thumbnail = Substitute.For<ISongHistoryThumbnailService>();

        // Signals once every song was processed, so the database is only read after the worker stopped using it
        var allProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processedCount = 0;
        var notifier = Substitute.For<ISongHistoryNotifier>();
        notifier.When(n => n.Publish(Arg.Any<long>(), SongHistoryNotificationKind.Processed)).Do(_ =>
        {
            if (Interlocked.Increment(ref processedCount) == songCount)
            {
                allProcessed.TrySetResult();
            }
        });

        // An interval no test would wait for: only batches processed back to back can drain the queue
        var (worker, _, _, _) = CreateWorker(
            thumbnail,
            snapshot,
            new Config
            {
                MusicRepositoryPath = "/data",
                SongHistoryWorkerEnabled = true,
                SongHistoryWorkerIntervalSeconds = 3600,
                SongHistoryWorkerBatchSize = 2,
            },
            notifier,
            CreateScopeFactory(scenario, _diffService, thumbnail, snapshot),
            scenario);

        for (var songId = 90; songId < 90 + songCount; songId++)
        {
            InsertQueueEntry(scenario, songId, revision: 1, BuildSnapshot(title: "Old", songId: songId));
            snapshot.GetCurrentSnapshotAsync(songId, true, Arg.Any<CancellationToken>())
                .Returns(BuildSnapshot(title: "New", songId: songId));
        }

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await allProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
        scenario.DbContext.SongHistories.Count().ShouldBe(songCount);
    }

    [Fact]
    public async Task ProcessQueue_EntriesDeletedWhileProcessing_ContinuesWithRemainingSongs()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();

        // The song's queue entries are removed mid-cycle, as deleting its owner does
        InsertQueueEntry(scenario, songId: 40, revision: 1, BuildSnapshot(title: "Gone Old", songId: 40));
        snapshot.GetCurrentSnapshotAsync(40, true, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                scenario.DbContext.SongHistoryQueues.Where(q => q.SongId == 40).ExecuteDelete();
                return BuildSnapshot(title: "Gone New", songId: 40);
            });

        InsertQueueEntry(scenario, songId: 41, revision: 1, BuildSnapshot(title: "Good Old", songId: 41));
        snapshot.GetCurrentSnapshotAsync(41, true, Arg.Any<CancellationToken>())
            .Returns(BuildSnapshot(title: "Good New", songId: 41));

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(1);

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(41);
    }

    [Fact]
    public async Task ProcessQueue_EntryWithErrorCountThree_IsSkipped()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var deadSnapshot = BuildSnapshot(title: "Dead", songId: 30, action: "updated");
        var deadEntry = InsertQueueEntry(scenario, songId: 30, revision: 1, deadSnapshot);
        deadEntry.ErrorCount = 3;
        deadEntry.LastError = "previous failure";
        scenario.DbContext.SaveChanges();

        var goodSnapshot = BuildSnapshot(title: "Good Old", songId: 31, action: "updated");
        var goodLive = BuildSnapshot(title: "Good New", songId: 31, action: "updated");
        InsertQueueEntry(scenario, songId: 31, revision: 1, goodSnapshot);
        snapshot.GetCurrentSnapshotAsync(31, true, Arg.Any<CancellationToken>()).Returns(goodLive);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var surviving = scenario.DbContext.SongHistoryQueues.Single();
        surviving.Id.ShouldBe(deadEntry.Id);
        surviving.ErrorCount.ShouldBe(3);

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(31);
    }

    [Fact]
    public async Task ProcessQueue_SameTransactionId_CompactsIntoSingleHistoryRow()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        const long txnId = 100;
        var liveState = BuildSnapshot(title: "V3", songId: 40, action: "updated");
        InsertQueueEntry(scenario, songId: 40, revision: 1, BuildSnapshot(title: "V1", songId: 40, action: "updated"), txnId);
        InsertQueueEntry(scenario, songId: 40, revision: 2, BuildSnapshot(title: "V2", songId: 40, action: "updated"), txnId);
        InsertQueueEntry(scenario, songId: 40, revision: 3, BuildSnapshot(title: "V3", songId: 40, action: "updated"), txnId);
        snapshot.GetCurrentSnapshotAsync(40, true, Arg.Any<CancellationToken>()).Returns(liveState);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(40);
        history.SongRevision.ShouldBe(1);
        history.Diff.Action.ShouldBe("updated");
        history.Diff.Title.ShouldNotBeNull();
        history.Diff.Title.Old.ShouldBe("V1");
        history.Diff.Title.New.ShouldBe("V3");

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_DifferentTransactionIds_ProducesSeparateHistoryRows()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        var liveState = BuildSnapshot(title: "T3", songId: 50, action: "updated");
        InsertQueueEntry(scenario, songId: 50, revision: 1, BuildSnapshot(title: "T1", songId: 50, action: "updated"), 200);
        InsertQueueEntry(scenario, songId: 50, revision: 2, BuildSnapshot(title: "T2", songId: 50, action: "updated"), 300);
        InsertQueueEntry(scenario, songId: 50, revision: 3, BuildSnapshot(title: "T3", songId: 50, action: "updated"), 400);
        snapshot.GetCurrentSnapshotAsync(50, true, Arg.Any<CancellationToken>()).Returns(liveState);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var histories = scenario.DbContext.SongHistories.OrderBy(h => h.SongRevision).ToList();
        histories.Count.ShouldBe(3);
        histories[0].Diff.Title.ShouldNotBeNull();
        histories[0].Diff.Title!.Old.ShouldBe("T1");
        histories[0].Diff.Title.New.ShouldBe("T2");
        histories[1].Diff.Title.ShouldNotBeNull();
        histories[1].Diff.Title!.Old.ShouldBe("T2");
        histories[1].Diff.Title.New.ShouldBe("T3");
        histories[2].Diff.Title.ShouldBeNull();

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_GroupFails_IncrementsErrorCountOnAllEntries()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        const long txnId = 500;
        InsertQueueEntry(scenario, songId: 60, revision: 1, BuildSnapshot(title: "V1", songId: 60, action: "updated"), txnId);
        InsertQueueEntry(scenario, songId: 60, revision: 2, BuildSnapshot(title: "V2", songId: 60, action: "updated"), txnId);
        snapshot.GetCurrentSnapshotAsync(60, true, Arg.Any<CancellationToken>())
            .Returns((Func<CallInfo, SongSnapshot>)(_ => throw new InvalidOperationException("DB unavailable for song 60")));

        var goodSnapshot = BuildSnapshot(title: "Good Old", songId: 61, action: "updated");
        var goodLive = BuildSnapshot(title: "Good New", songId: 61, action: "updated");
        InsertQueueEntry(scenario, songId: 61, revision: 1, goodSnapshot);
        snapshot.GetCurrentSnapshotAsync(61, true, Arg.Any<CancellationToken>()).Returns(goodLive);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(1);

        var survivingQueue = scenario.DbContext.SongHistoryQueues
            .Where(q => q.SongId == 60)
            .OrderBy(q => q.SongRevision)
            .ToList();
        survivingQueue.Count.ShouldBe(2);
        survivingQueue.ShouldAllBe(q => q.ErrorCount == 1);
        survivingQueue.ShouldAllBe(q => q.LastError != null);

        var history = scenario.DbContext.SongHistories.Single();
        history.SongId.ShouldBe(61);
        history.Diff.Title.ShouldNotBeNull();
        history.Diff.Title!.Old.ShouldBe("Good Old");
        history.Diff.Title.New.ShouldBe("Good New");
    }

    [Fact]
    public async Task ProcessQueue_CreatedTransaction_WritesBaselineWithEveryField()
    {
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        const long txnId = 200;
        var artists = new List<SongSnapshotArtist> { new() { Id = 3, Name = "Artist" } };
        // The song row is inserted first, then its artists, all in the same transaction
        InsertQueueEntry(scenario, songId: 60, revision: 1, BuildSnapshot(title: "New Song", songId: 60, action: "created"), txnId);
        InsertQueueEntry(scenario, songId: 60, revision: 2, BuildSnapshot(title: "New Song", songId: 60, action: "updated"), txnId);
        var liveState = BuildSnapshot(title: "New Song", songId: 60, action: "updated", artists: artists);
        snapshot.GetCurrentSnapshotAsync(60, true, Arg.Any<CancellationToken>()).Returns(liveState);

        var (processed, failed) = await worker.ProcessQueueAsync(
            scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        processed.ShouldBe(1);
        failed.ShouldBe(0);

        var history = scenario.DbContext.SongHistories.Single();
        history.SongRevision.ShouldBe(1);
        history.Action.ShouldBe("created");
        history.Diff.Action.ShouldBe("created");
        history.Diff.Title.ShouldNotBeNull();
        history.Diff.Title.Old.ShouldBeNull();
        history.Diff.Title.New.ShouldBe("New Song");
        // Fields holding default values are recorded too, so the baseline shows the full state
        history.Diff.Explicit.ShouldNotBeNull();
        history.Diff.Explicit.New.ShouldBeFalse();
        history.Diff.Artists.ShouldNotBeNull();
        history.Diff.Artists.Old.ShouldBeNull();
        history.Diff.Artists.New.ShouldNotBeNull().Select(a => a.Name).ShouldBe(["Artist"]);

        scenario.DbContext.SongHistoryQueues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessQueue_CreatedThenEdited_BaselineLoadsCoverMissingFromSnapshot()
    {
        var (base64, mimeType) = MakeCoverImage(400, 400);
        var cover = new SongSnapshotCover { Id = 7, MimeType = mimeType, Width = 400, Height = 400, Data = base64 };
        var (worker, scenario, thumbnail, snapshot) = CreateWorker();
        InsertQueueEntry(scenario, songId: 70, revision: 1, BuildSnapshot(title: "V1", songId: 70, action: "created"), 1);
        // The edit's pre-change snapshot references the cover without carrying it (cover_id did not change)
        InsertQueueEntry(scenario, songId: 70, revision: 2,
            BuildSnapshot(title: "V1", songId: 70, action: "updated") with { CoverId = 7 }, 2);
        snapshot.GetCurrentSnapshotAsync(70, true, Arg.Any<CancellationToken>())
            .Returns(BuildSnapshot(title: "V2", songId: 70, action: "updated", cover: cover));
        snapshot.GetCoverAsync(7, Arg.Any<CancellationToken>()).Returns(cover);

        await worker.ProcessQueueAsync(scenario.DbContext, _diffService, thumbnail, snapshot, CancellationToken.None);

        var histories = scenario.DbContext.SongHistories.OrderBy(h => h.SongRevision).ToList();
        histories.Count.ShouldBe(2);
        histories[0].Action.ShouldBe("created");
        histories[0].Diff.Title.ShouldNotBeNull().New.ShouldBe("V1");
        var baselineCover = histories[0].Diff.Cover.ShouldNotBeNull().New.ShouldNotBeNull();
        baselineCover.Id.ShouldBe(7);
        // Stored as a thumbnail, like every cover in history
        baselineCover.Data.ShouldNotBe(base64);
        histories[1].Action.ShouldBe("updated");
        histories[1].Diff.Title.ShouldNotBeNull().Old.ShouldBe("V1");
        histories[1].Diff.Cover.ShouldBeNull();
    }
}
