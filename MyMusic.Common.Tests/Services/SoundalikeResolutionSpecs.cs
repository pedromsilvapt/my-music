using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Services.BackgroundJobs;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SoundalikeResolutionSpecs
{
    private static readonly IOptions<Config> ConfigOptions = Options.Create(new Config { MusicRepositoryPath = "/data" });

    private SoundalikeResolutionService CreateService(
        ISoundalikeMergeService? mergeService = null,
        ISongFileUpdateService? songFileUpdate = null,
        IFileTransactionService? fileTransactions = null)
    {
        mergeService ??= Substitute.For<ISoundalikeMergeService>();
        songFileUpdate ??= CreateSongFileUpdate(checksumChanged: false);
        fileTransactions ??= Substitute.For<IFileTransactionService>();
        var logger = Substitute.For<ILogger<SoundalikeResolutionService>>();
        return new SoundalikeResolutionService(mergeService, songFileUpdate, fileTransactions, ConfigOptions, logger);
    }

    private static ISongFileUpdateService CreateSongFileUpdate(bool checksumChanged)
    {
        var songFileUpdate = Substitute.For<ISongFileUpdateService>();
        songFileUpdate
            .UpdateAsync(Arg.Any<MusicDbContext>(), Arg.Any<IFileTransaction>(), Arg.Any<Song>(),
                Arg.Any<Func<string>>(), Arg.Any<CancellationToken>())
            .Returns(new SongFileUpdateResult { PreviousPath = null, ChecksumChanged = checksumChanged });
        return songFileUpdate;
    }

    [Fact]
    public async Task Resolve_SecondaryInPlaylist_PrimaryNotInPlaylist_AddsPrimaryToPlaylist()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist = scenario.CreatePlaylist("Playlist");
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var playlistSongs = scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlist.Id)
            .OrderBy(ps => ps.Order)
            .ToList();
        playlistSongs.Count.ShouldBe(1);
        playlistSongs[0].SongId.ShouldBe(primary.Id);
        playlistSongs[0].Order.ShouldBe(1);
    }

    [Fact]
    public async Task Resolve_SecondaryInPlaylist_PrimaryAlreadyInPlaylist_DoesNotDuplicate()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist = scenario.CreatePlaylist("Playlist");
        scenario.AddSongToPlaylist(playlist, primary, order: 0);
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var playlistSongs = scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlist.Id)
            .ToList();
        playlistSongs.Count.ShouldBe(1);
        playlistSongs[0].SongId.ShouldBe(primary.Id);
    }

    [Fact]
    public async Task Resolve_SecondaryInMultiplePlaylists_PrimaryNotInAny_AddsToAll()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist1 = scenario.CreatePlaylist("Playlist1");
        var playlist2 = scenario.CreatePlaylist("Playlist2");
        scenario.AddSongToPlaylist(playlist1, secondary, order: 1);
        scenario.AddSongToPlaylist(playlist2, secondary, order: 2);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var ps1 = scenario.DbContext.PlaylistSongs.Where(ps => ps.PlaylistId == playlist1.Id).ToList();
        var ps2 = scenario.DbContext.PlaylistSongs.Where(ps => ps.PlaylistId == playlist2.Id).ToList();
        ps1.Count.ShouldBe(1);
        ps1[0].SongId.ShouldBe(primary.Id);
        ps2.Count.ShouldBe(1);
        ps2[0].SongId.ShouldBe(primary.Id);
    }

    [Fact]
    public async Task Resolve_SecondaryInPlaylist_PrimaryInSome_AddsOnlyToMissing()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist1 = scenario.CreatePlaylist("Playlist1");
        var playlist2 = scenario.CreatePlaylist("Playlist2");
        scenario.AddSongToPlaylist(playlist1, primary, order: 0);
        scenario.AddSongToPlaylist(playlist1, secondary, order: 1);
        scenario.AddSongToPlaylist(playlist2, secondary, order: 0);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var ps1 = scenario.DbContext.PlaylistSongs.Where(ps => ps.PlaylistId == playlist1.Id).ToList();
        var ps2 = scenario.DbContext.PlaylistSongs.Where(ps => ps.PlaylistId == playlist2.Id).ToList();
        ps1.Count.ShouldBe(1);
        ps1[0].SongId.ShouldBe(primary.Id);
        ps2.Count.ShouldBe(1);
        ps2[0].SongId.ShouldBe(primary.Id);
    }

    [Fact]
    public async Task Resolve_PlaylistCurrentSongIsSecondary_RedirectsToPrimary()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist = scenario.CreatePlaylist("Playlist", currentSongId: secondary.Id);
        scenario.AddSongToPlaylist(playlist, primary, order: 0);
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.Entry(playlist).Reload();
        playlist.CurrentSongId.ShouldBe(primary.Id);
    }

    [Fact]
    public async Task Resolve_MultipleSecondariesInSamePlaylist_AddsPrimaryOnceAtLowestOrder()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var sec1 = scenario.CreateSong("Secondary1");
        var sec2 = scenario.CreateSong("Secondary2");
        var playlist = scenario.CreatePlaylist("Playlist");
        scenario.AddSongToPlaylist(playlist, sec1, order: 1);
        scenario.AddSongToPlaylist(playlist, sec2, order: 3);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions =
            [
                new SecondarySongActionInput { SongId = sec1.Id, Action = SecondaryAction.Delete },
                new SecondarySongActionInput { SongId = sec2.Id, Action = SecondaryAction.Delete }
            ]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var playlistSongs = scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlist.Id)
            .OrderBy(ps => ps.Order)
            .ToList();
        playlistSongs.Count.ShouldBe(1);
        playlistSongs[0].SongId.ShouldBe(primary.Id);
        playlistSongs[0].Order.ShouldBe(1);
    }

    [Fact]
    public async Task Resolve_SecondaryOnDevice_PrimaryNotOnDevice_CreatesSongDeviceForPrimary()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var device = scenario.CreateDevice("Phone");
        scenario.CreateSongDevice(device, secondary, "/music/Secondary.mp3");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var primaryDevice = scenario.DbContext.SongDevices
            .FirstOrDefault(sd => sd.SongId == primary.Id && sd.DeviceId == device.Id);
        primaryDevice.ShouldNotBeNull();
        primaryDevice.SyncAction.ShouldBe(SongSyncAction.Download);
        primaryDevice.DevicePath.ShouldNotBeNullOrEmpty();

        var secondaryDevice = scenario.DbContext.SongDevices
            .FirstOrDefault(sd => sd.DeviceId == device.Id && sd.DevicePath == "/music/Secondary.mp3");
        secondaryDevice.ShouldNotBeNull();
        secondaryDevice.SongId.ShouldBeNull();
        secondaryDevice.SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task Resolve_SecondaryOnDevice_PrimaryAlreadyOnDevice_MarksSecondaryForRemove()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var device = scenario.CreateDevice("Phone");
        scenario.CreateSongDevice(device, primary, "/music/Primary.mp3");
        scenario.CreateSongDevice(device, secondary, "/music/Secondary.mp3");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var primaryDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == primary.Id && sd.DeviceId == device.Id)
            .ToList();
        primaryDevices.Count.ShouldBe(1);

        var secondaryDevice = scenario.DbContext.SongDevices
            .FirstOrDefault(sd => sd.DeviceId == device.Id && sd.DevicePath == "/music/Secondary.mp3");
        secondaryDevice.ShouldNotBeNull();
        secondaryDevice.SongId.ShouldBeNull();
        secondaryDevice.SyncAction.ShouldBe(SongSyncAction.Remove);
    }

    [Fact]
    public async Task Resolve_SecondaryOnMultipleDevices_PrimaryNotOnAny_CreatesSongDevicesForAll()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var device1 = scenario.CreateDevice("Phone");
        var device2 = scenario.CreateDevice("Tablet");
        scenario.CreateSongDevice(device1, secondary, "/music/Secondary.mp3");
        scenario.CreateSongDevice(device2, secondary, "/music/Secondary.mp3");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var primaryDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == primary.Id)
            .ToList();
        primaryDevices.Count.ShouldBe(2);
        primaryDevices.All(sd => sd.SyncAction == SongSyncAction.Download).ShouldBeTrue();
    }

    [Fact]
    public async Task Resolve_MixedPlaylistAndDeviceTransfer()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist = scenario.CreatePlaylist("Playlist");
        var device = scenario.CreateDevice("Phone");
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);
        scenario.CreateSongDevice(device, secondary, "/music/Secondary.mp3");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var playlistSongs = scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlist.Id).ToList();
        playlistSongs.Count.ShouldBe(1);
        playlistSongs[0].SongId.ShouldBe(primary.Id);

        var primaryDevice = scenario.DbContext.SongDevices
            .FirstOrDefault(sd => sd.SongId == primary.Id && sd.DeviceId == device.Id);
        primaryDevice.ShouldNotBeNull();
        primaryDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task Resolve_MergeAction_MergesMetadataBeforeTransfer()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = Substitute.For<ISoundalikeMergeService>();
        var service = CreateService(mergeService);
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist = scenario.CreatePlaylist("Playlist");
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Merge }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        await mergeService.Received(1).MergeMetadataAsync(
            scenario.DbContext,
            Arg.Is<Song>(s => s.Id == primary.Id),
            Arg.Is<List<Song>>(l => l.Any(s => s.Id == secondary.Id)),
            Arg.Any<CancellationToken>());

        var playlistSongs = scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlist.Id).ToList();
        playlistSongs.Count.ShouldBe(1);
        playlistSongs[0].SongId.ShouldBe(primary.Id);
    }

    [Fact]
    public async Task Resolve_IgnoreAction_DoesNotDeleteOrTransfer()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var playlist = scenario.CreatePlaylist("Playlist");
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Ignore }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var playlistSongs = scenario.DbContext.PlaylistSongs
            .Where(ps => ps.PlaylistId == playlist.Id).ToList();
        playlistSongs.Count.ShouldBe(1);
        playlistSongs[0].SongId.ShouldBe(secondary.Id);

        var secondaryStillExists = scenario.DbContext.Songs.Any(s => s.Id == secondary.Id);
        secondaryStillExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Resolve_DeletesSecondarySongs()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.Songs.Any(s => s.Id == secondary.Id).ShouldBeFalse();
        scenario.DbContext.Songs.Any(s => s.Id == primary.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Resolve_RemovesNonConformity()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var nc = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = nc.Id,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.AuditNonConformities.Any(nc => nc.Id == nc.Id).ShouldBeFalse();
    }

    [Theory]
    [InlineData(SecondaryAction.Delete)]
    [InlineData(SecondaryAction.Merge)]
    public async Task Resolve_SecondaryHasOwnNonConformities_RemovesThemWithTheSong(SecondaryAction action)
    {
        // Arrange: both songs were also flagged by another audit rule
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var group = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id);
        var primaryOwn = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id, ruleId: 2, songId: primary.Id);
        var secondaryOwn = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id, ruleId: 2, songId: secondary.Id);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = group.Id,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = action }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.Songs.Any(s => s.Id == secondary.Id).ShouldBeFalse();
        scenario.DbContext.AuditNonConformities.Any(n => n.Id == secondaryOwn.Id).ShouldBeFalse();
        scenario.DbContext.AuditNonConformities.Any(n => n.Id == primaryOwn.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Resolve_SecondaryWasPurchased_PointsThePurchaseToThePrimary()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var purchase = scenario.CreatePurchase(scenario.CreateSource(), scenario.AdminUser.Id,
            PurchasedSongStatus.Completed);
        purchase.SongId = secondary.Id;
        scenario.DbContext.SaveChanges();

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.Songs.Any(s => s.Id == secondary.Id).ShouldBeFalse();
        scenario.DbContext.PurchasedSongs.Single(p => p.Id == purchase.Id).SongId.ShouldBe(primary.Id);
    }

    [Fact]
    public async Task Resolve_ReturnsResolvedCount()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var p1 = scenario.CreateSong("Primary1");
        var s1 = scenario.CreateSong("Sec1");
        var p2 = scenario.CreateSong("Primary2");
        var s2 = scenario.CreateSong("Sec2");
        var nc1 = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id);
        var nc2 = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id);

        // Act
        var result = await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id,
        [
            new GroupResolutionInput
            {
                NonConformityId = nc1.Id, PrimarySongId = p1.Id,
                SecondaryActions = [new SecondarySongActionInput { SongId = s1.Id, Action = SecondaryAction.Delete }]
            },
            new GroupResolutionInput
            {
                NonConformityId = nc2.Id, PrimarySongId = p2.Id,
                SecondaryActions = [new SecondarySongActionInput { SongId = s2.Id, Action = SecondaryAction.Delete }]
            }
        ]);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public async Task Resolve_PrimaryOnDevice_SecondaryOnDifferentDevice_AddsPrimaryToSecondaryDevice()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var device1 = scenario.CreateDevice("Phone");
        var device2 = scenario.CreateDevice("Tablet");
        scenario.CreateSongDevice(device1, primary, "/music/Primary.mp3");
        scenario.CreateSongDevice(device2, secondary, "/music/Secondary.mp3");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var primaryDevices = scenario.DbContext.SongDevices
            .Where(sd => sd.SongId == primary.Id)
            .ToList();
        primaryDevices.Count.ShouldBe(2);
        primaryDevices.Select(sd => sd.DeviceId).ShouldContain(device1.Id);
        primaryDevices.Select(sd => sd.DeviceId).ShouldContain(device2.Id);

        var newDevice = primaryDevices.First(sd => sd.DeviceId == device2.Id);
        newDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task Resolve_RecordsMergeOfEverySecondaryWithItsChosenAction()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var merged = scenario.CreateSong("Merged");
        var deleted = scenario.CreateSong("Deleted");
        var kept = scenario.CreateSong("Kept");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions =
            [
                new SecondarySongActionInput { SongId = merged.Id, Action = SecondaryAction.Merge },
                new SecondarySongActionInput { SongId = deleted.Id, Action = SecondaryAction.Delete },
                new SecondarySongActionInput { SongId = kept.Id, Action = SecondaryAction.Ignore },
            ]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert: one merge per removed secondary, none for the one that was kept
        var merges = scenario.DbContext.SongMerges.OrderBy(m => m.MergedSongId).ToList();
        merges.Select(m => (m.KeptSongId, m.MergedSongId, m.Kind)).ShouldBe(
        [
            (primary.Id, merged.Id, SongMergeKind.SoundalikeMerge),
            (primary.Id, deleted.Id, SongMergeKind.SoundalikeDelete),
        ]);
        merges.ShouldAllBe(m => m.OwnerId == scenario.AdminUser.Id);
    }

    [Fact]
    public async Task Resolve_SecondaryNotFound_RecordsNoMerge()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = 9999, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.SongMerges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resolve_IgnoreAction_ExcludesThePairAndRemovesNonConformity()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var ignored = scenario.CreateSong("Ignored");
        var nc = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = nc.Id,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = ignored.Id, Action = SecondaryAction.Ignore }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var pair = scenario.DbContext.ExcludedDuplicatePairs.ShouldHaveSingleItem();
        pair.SongAId.ShouldBe(Math.Min(primary.Id, ignored.Id));
        pair.SongBId.ShouldBe(Math.Max(primary.Id, ignored.Id));
        pair.OwnerId.ShouldBe(scenario.AdminUser.Id);

        scenario.DbContext.SongMerges.ShouldBeEmpty();
        scenario.DbContext.AuditNonConformities.Any(n => n.Id == nc.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task Resolve_IgnoreAction_PairAlreadyExcluded_DoesNotDuplicate()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var ignored = scenario.CreateSong("Ignored");
        scenario.DbContext.ExcludedDuplicatePairs.Add(new ExcludedDuplicatePair
        {
            SongAId = Math.Min(primary.Id, ignored.Id),
            SongBId = Math.Max(primary.Id, ignored.Id),
            OwnerId = scenario.AdminUser.Id,
        });
        scenario.DbContext.SaveChanges();

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = ignored.Id, Action = SecondaryAction.Ignore }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        scenario.DbContext.ExcludedDuplicatePairs.Count().ShouldBe(1);
    }

    [Fact]
    public async Task Resolve_KeepsOldestDatesOfMergedAndDeletedSongs_ButNotOfIgnoredOnes()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var merged = scenario.CreateSong("Merged");
        var deleted = scenario.CreateSong("Deleted");
        var ignored = scenario.CreateSong("Ignored");
        SetDates(scenario, primary, createdAt: Utc(2020), addedAt: Utc(2021), modifiedAt: Utc(2021));
        SetDates(scenario, merged, createdAt: Utc(2015), addedAt: Utc(2022), modifiedAt: Utc(2022));
        SetDates(scenario, deleted, createdAt: Utc(2018), addedAt: Utc(2019), modifiedAt: Utc(2019));
        SetDates(scenario, ignored, createdAt: Utc(2010), addedAt: Utc(2010), modifiedAt: Utc(2010));
        var before = DateTime.UtcNow;

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions =
            [
                new SecondarySongActionInput { SongId = merged.Id, Action = SecondaryAction.Merge },
                new SecondarySongActionInput { SongId = deleted.Id, Action = SecondaryAction.Delete },
                new SecondarySongActionInput { SongId = ignored.Id, Action = SecondaryAction.Ignore },
            ]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var kept = scenario.DbContext.Songs.First(s => s.Id == primary.Id);
        kept.CreatedAt.ShouldBe(Utc(2015));
        kept.AddedAt.ShouldBe(Utc(2019));
        kept.ModifiedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task Resolve_PrimaryIsTheOldest_KeepsItsDates()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        SetDates(scenario, primary, createdAt: Utc(2015), addedAt: null, modifiedAt: Utc(2016));
        SetDates(scenario, secondary, createdAt: Utc(2018), addedAt: Utc(2019), modifiedAt: Utc(2019));
        var before = DateTime.UtcNow;

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert: a missing added date does not count as the oldest one
        var kept = scenario.DbContext.Songs.First(s => s.Id == primary.Id);
        kept.CreatedAt.ShouldBe(Utc(2015));
        kept.AddedAt.ShouldBe(Utc(2019));
        kept.ModifiedAt.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task Resolve_OnlyIgnoredSongs_DoesNotTouchTheDates()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong("Primary");
        var ignored = scenario.CreateSong("Ignored");
        SetDates(scenario, primary, createdAt: Utc(2020), addedAt: Utc(2021), modifiedAt: Utc(2021));
        SetDates(scenario, ignored, createdAt: Utc(2010), addedAt: Utc(2010), modifiedAt: Utc(2010));

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = ignored.Id, Action = SecondaryAction.Ignore }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var kept = scenario.DbContext.Songs.First(s => s.Id == primary.Id);
        kept.CreatedAt.ShouldBe(Utc(2020));
        kept.AddedAt.ShouldBe(Utc(2021));
        kept.ModifiedAt.ShouldBe(Utc(2021));
    }

    [Fact]
    public async Task Resolve_MergeAction_FileChecksumChanges_UpdatesFileModifiedAtAndMarksDevicesForDownload()
    {
        // Arrange: the kept song's file lacks the genre the merged song brings
        var scenario = new Scenario();
        var service = CreateService(
            new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>()),
            new SongFileUpdateService(scenario.FileSystem, ConfigOptions),
            scenario.FileTransactions);
        scenario.FileSystem.Directory.CreateDirectory("/data/admin");
        MockMusicFile.Create(scenario.FileSystem, "/data/admin/Primary.mp3", "Primary", "Album", ["Artist"], ["Rock"]);
        var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
        var checksum = ChecksumService.CalculateChecksum(scenario.FileSystem, checksumAlgorithm, "/data/admin/Primary.mp3");
        var primary = scenario.CreateSong("Primary", repositoryPath: "/data/admin/Primary.mp3", checksum: checksum,
            checksumAlgorithm: checksumAlgorithm.GetType().Name, fileModifiedAt: Utc(2020),
            genres: [scenario.CreateGenre("Rock")]);
        var secondary = scenario.CreateSong("Secondary", genres: [scenario.CreateGenre("Jazz")]);
        var device = scenario.CreateDevice("Phone");
        scenario.CreateSongDevice(device, primary, "/music/Primary.mp3");
        var before = DateTime.UtcNow;

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Merge }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        var kept = scenario.DbContext.Songs.First(s => s.Id == primary.Id);
        kept.Checksum.ShouldNotBe(checksum);
        kept.Checksum.ShouldBe(ChecksumService.CalculateChecksum(scenario.FileSystem,
            ChecksumService.CreateChecksumAlgorithm(), kept.RepositoryPath));
        kept.FileModifiedAt.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(before);

        var songDevice = scenario.DbContext.SongDevices.First(sd => sd.SongId == primary.Id);
        songDevice.SyncAction.ShouldBe(SongSyncAction.Download);
    }

    [Fact]
    public async Task Resolve_MergeAction_FileChecksumUnchanged_KeepsFileModifiedAt()
    {
        // Arrange
        var scenario = new Scenario();
        var songFileUpdate = CreateSongFileUpdate(checksumChanged: false);
        var service = CreateService(songFileUpdate: songFileUpdate);
        var primary = scenario.CreateSong("Primary", fileModifiedAt: Utc(2020));
        var secondary = scenario.CreateSong("Secondary");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Merge }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        await songFileUpdate.Received(1).UpdateAsync(scenario.DbContext, Arg.Any<IFileTransaction>(),
            Arg.Is<Song>(s => s.Id == primary.Id), Arg.Any<Func<string>>(), Arg.Any<CancellationToken>());
        scenario.DbContext.Songs.First(s => s.Id == primary.Id).FileModifiedAt.ShouldBe(Utc(2020));
    }

    [Fact]
    public async Task Resolve_DeleteAction_DoesNotWriteTheFile()
    {
        // Arrange
        var scenario = new Scenario();
        var songFileUpdate = CreateSongFileUpdate(checksumChanged: false);
        var service = CreateService(songFileUpdate: songFileUpdate);
        var primary = scenario.CreateSong("Primary", fileModifiedAt: Utc(2020));
        var secondary = scenario.CreateSong("Secondary");

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Delete }]
        };

        // Act
        await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert
        await songFileUpdate.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default!, default!, default!);
        scenario.DbContext.Songs.First(s => s.Id == primary.Id).FileModifiedAt.ShouldBe(Utc(2020));
    }

    [Fact]
    public async Task Resolve_WithoutNonConformity_MergesTheSongsAndLeavesNonConformitiesAlone()
    {
        // Arrange: songs picked by the user rather than detected, next to an unrelated soundalike group
        var scenario = new Scenario();
        var mergeService = Substitute.For<ISoundalikeMergeService>();
        var service = CreateService(mergeService);
        var primary = scenario.CreateSong("Primary");
        var secondary = scenario.CreateSong("Secondary");
        var unrelated = CreateNonConformity(scenario.DbContext, scenario.AdminUser.Id);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = null,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Merge }]
        };

        // Act
        var resolved = await service.ResolveAsync(scenario.DbContext, scenario.AdminUser.Id, [resolution]);

        // Assert: the secondary is merged away like a detected soundalike would be
        resolved.ShouldBe(1);
        await mergeService.Received(1).MergeMetadataAsync(
            scenario.DbContext,
            Arg.Is<Song>(s => s.Id == primary.Id),
            Arg.Is<List<Song>>(l => l.Any(s => s.Id == secondary.Id)),
            Arg.Any<CancellationToken>());
        scenario.DbContext.Songs.Select(s => s.Id).ToList().ShouldBe([primary.Id]);
        scenario.DbContext.SongMerges.ShouldHaveSingleItem().Kind.ShouldBe(SongMergeKind.SoundalikeMerge);
        scenario.DbContext.AuditNonConformities.Select(nc => nc.Id).ToList().ShouldBe([unrelated.Id]);
    }

    #region Helper Methods

    private static DateTime Utc(int year) => new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static void SetDates(Scenario scenario, Song song, DateTime createdAt, DateTime? addedAt, DateTime modifiedAt)
    {
        song.CreatedAt = createdAt;
        song.AddedAt = addedAt;
        song.ModifiedAt = modifiedAt;
        scenario.DbContext.SaveChanges();
    }

    private AuditNonConformity CreateNonConformity(MusicDbContext db, long ownerId, long ruleId = 9, long? songId = null)
    {
        var nc = new AuditNonConformity
        {
            AuditRuleId = ruleId,
            SongId = songId,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(nc);
        db.SaveChanges();
        return nc;
    }

    #endregion
}
