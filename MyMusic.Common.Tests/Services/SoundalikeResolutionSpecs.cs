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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, secondary, order: 1);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, primary, order: 0);
        AddSongToPlaylist(scenario.DbContext, playlist, secondary, order: 1);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist1 = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist1");
        var playlist2 = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist2");
        AddSongToPlaylist(scenario.DbContext, playlist1, secondary, order: 1);
        AddSongToPlaylist(scenario.DbContext, playlist2, secondary, order: 2);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist1 = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist1");
        var playlist2 = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist2");
        AddSongToPlaylist(scenario.DbContext, playlist1, primary, order: 0);
        AddSongToPlaylist(scenario.DbContext, playlist1, secondary, order: 1);
        AddSongToPlaylist(scenario.DbContext, playlist2, secondary, order: 0);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist", currentSongId: secondary.Id);
        AddSongToPlaylist(scenario.DbContext, playlist, primary, order: 0);
        AddSongToPlaylist(scenario.DbContext, playlist, secondary, order: 1);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var sec1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary1");
        var sec2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary2");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, sec1, order: 1);
        AddSongToPlaylist(scenario.DbContext, playlist, sec2, order: 3);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, secondary, device, "/music/Secondary.mp3");

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToDevice(scenario.DbContext, primary, device, "/music/Primary.mp3");
        AddSongToDevice(scenario.DbContext, secondary, device, "/music/Secondary.mp3");

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var device1 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var device2 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Tablet");
        AddSongToDevice(scenario.DbContext, secondary, device1, "/music/Secondary.mp3");
        AddSongToDevice(scenario.DbContext, secondary, device2, "/music/Secondary.mp3");

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist");
        var device = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        AddSongToPlaylist(scenario.DbContext, playlist, secondary, order: 1);
        AddSongToDevice(scenario.DbContext, secondary, device, "/music/Secondary.mp3");

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, secondary, order: 1);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var playlist = CreatePlaylist(scenario.DbContext, scenario.AdminUser.Id, "Playlist");
        AddSongToPlaylist(scenario.DbContext, playlist, secondary, order: 1);

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");

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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
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
        var p1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary1");
        var s1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Sec1");
        var p2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary2");
        var s2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Sec2");
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
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary");
        var device1 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Phone");
        var device2 = CreateDevice(scenario.DbContext, scenario.AdminUser.Id, "Tablet");
        AddSongToDevice(scenario.DbContext, primary, device1, "/music/Primary.mp3");
        AddSongToDevice(scenario.DbContext, secondary, device2, "/music/Secondary.mp3");

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

    private Song CreateSong(MusicDbContext db, long ownerId, string title)
    {
        var artist = new Artist
        {
            Name = $"{title} Artist",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(artist);
        db.SaveChanges();

        var album = new Album
        {
            Name = $"{title} Album",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(album);
        db.SaveChanges();

        var song = new Song
        {
            Title = title,
            Label = title,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            AlbumId = album.Id,
            Album = album,
            Duration = TimeSpan.FromSeconds(180),
            Size = 5000000,
            RepositoryPath = $"/music/{title}.mp3",
            Checksum = $"checksum-{title}",
            ChecksumAlgorithm = "MD5",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = []
        };
        db.Add(song);
        db.SaveChanges();

        var songArtist = new SongArtist
        {
            SongId = song.Id,
            ArtistId = artist.Id,
            Artist = artist,
            Song = song
        };
        db.Add(songArtist);
        db.SaveChanges();

        return song;
    }

    private Playlist CreatePlaylist(MusicDbContext db, long ownerId, string name, long? currentSongId = null)
    {
        var playlist = new Playlist
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            CurrentSongId = currentSongId,
            PlaylistSongs = []
        };
        db.Add(playlist);
        db.SaveChanges();
        return playlist;
    }

    private void AddSongToPlaylist(MusicDbContext db, Playlist playlist, Song song, double order)
    {
        var ps = new PlaylistSong
        {
            PlaylistId = playlist.Id,
            SongId = song.Id,
            Order = order,
            AddedAt = DateTime.UtcNow
        };
        db.Add(ps);
        db.SaveChanges();
    }

    private Device CreateDevice(MusicDbContext db, long ownerId, string name)
    {
        var device = new Device
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            Songs = []
        };
        db.Add(device);
        db.SaveChanges();
        return device;
    }

    private void AddSongToDevice(MusicDbContext db, Song song, Device device, string path)
    {
        var sd = new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = path,
            AddedAt = DateTime.UtcNow
        };
        db.Add(sd);
        db.SaveChanges();
    }

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
