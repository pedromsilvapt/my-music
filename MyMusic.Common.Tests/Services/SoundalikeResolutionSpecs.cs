using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SoundalikeResolutionSpecs
{
    private SoundalikeResolutionService CreateService(ISoundalikeMergeService? mergeService = null)
    {
        mergeService ??= Substitute.For<ISoundalikeMergeService>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        var logger = Substitute.For<ILogger<SoundalikeResolutionService>>();
        return new SoundalikeResolutionService(mergeService, config, logger);
    }

    [Fact]
    public async Task Resolve_SecondaryInPlaylist_PrimaryNotInPlaylist_AddsPrimaryToPlaylist()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var sec1 = scenario.CreateSong( "Secondary1");
        var sec2 = scenario.CreateSong( "Secondary2");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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
    public async Task Resolve_KeepAction_DoesNotDeleteOrTransfer()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
        var playlist = scenario.CreatePlaylist("Playlist");
        scenario.AddSongToPlaylist(playlist, secondary, order: 1);

        var resolution = new GroupResolutionInput
        {
            NonConformityId = 1,
            PrimarySongId = primary.Id,
            SecondaryActions = [new SecondarySongActionInput { SongId = secondary.Id, Action = SecondaryAction.Keep }]
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");

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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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

    [Fact]
    public async Task Resolve_ReturnsResolvedCount()
    {
        // Arrange
        var scenario = new Scenario();
        var service = CreateService();
        var p1 = scenario.CreateSong( "Primary1");
        var s1 = scenario.CreateSong( "Sec1");
        var p2 = scenario.CreateSong( "Primary2");
        var s2 = scenario.CreateSong( "Sec2");
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
        var primary = scenario.CreateSong( "Primary");
        var secondary = scenario.CreateSong( "Secondary");
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

    #region Helper Methods


    private AuditNonConformity CreateNonConformity(MusicDbContext db, long ownerId)
    {
        var nc = new AuditNonConformity
        {
            AuditRuleId = 9,
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
