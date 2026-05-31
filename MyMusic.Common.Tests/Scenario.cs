using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Seeding;
using MyMusic.Common.Services;
using NSubstitute;

namespace MyMusic.Common.Tests;

public class Scenario
{
    public Scenario()
    {
        FileSystem = CreateFileSystem();
        DbContext = CreateDbContext();
        AdminUser = CreateUser("Administrator", "admin");
    }

    public IFileSystem FileSystem { get; set; }

    public MusicDbContext DbContext { get; set; }

    public User AdminUser { get; set; }

    #region Seeding Data

    public User CreateUser(string name, string username)
    {
        var user = new User
        {
            Name = name,
            Username = username,
        };

        DbContext.Add(user);
        DbContext.SaveChanges();

        return user;
    }

    public Artist CreateArtist(string name, long? ownerId = null)
    {
        ownerId ??= AdminUser.Id;
        var owner = DbContext.Users.First(u => u.Id == ownerId);
        var artist = new Artist
        {
            Name = name,
            OwnerId = ownerId.Value,
            Owner = owner,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.Add(artist);
        DbContext.SaveChanges();
        return artist;
    }

    public Album CreateAlbum(string name, Artist artist, long? ownerId = null)
    {
        ownerId ??= AdminUser.Id;
        var owner = DbContext.Users.First(u => u.Id == ownerId);
        var album = new Album
        {
            Name = name,
            ArtistId = artist.Id,
            Artist = artist,
            OwnerId = ownerId.Value,
            Owner = owner,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.Add(album);
        DbContext.SaveChanges();
        return album;
    }

    public Device CreateDevice(string? name = null, long? ownerId = null, string? namingTemplate = null)
    {
        ownerId ??= AdminUser.Id;
        var owner = DbContext.Users.First(u => u.Id == ownerId);
        var device = new Device
        {
            Name = name ?? $"Device-{Guid.NewGuid():N}",
            OwnerId = ownerId.Value,
            Owner = owner,
            Songs = [],
        };
        if (namingTemplate != null)
            device.NamingTemplate = namingTemplate;
        DbContext.Add(device);
        DbContext.SaveChanges();
        return device;
    }

    public Genre CreateGenre(string name, long? ownerId = null)
    {
        ownerId ??= AdminUser.Id;
        var owner = DbContext.Users.First(u => u.Id == ownerId);
        var genre = new Genre
        {
            Name = name,
            OwnerId = ownerId.Value,
            Owner = owner,
        };
        DbContext.Add(genre);
        DbContext.SaveChanges();
        return genre;
    }

    public Song CreateSong(
        string title,
        long? ownerId = null,
        string? repositoryPath = null,
        string? checksum = null,
        string? checksumAlgorithm = null,
        int? bitrate = null,
        DateTime? modifiedAt = null,
        int? year = null,
        string? lyrics = null,
        long? coverId = null,
        List<Artist>? artists = null,
        List<Genre>? genres = null,
        Album? album = null)
    {
        ownerId ??= AdminUser.Id;
        var owner = DbContext.Users.First(u => u.Id == ownerId);

        Artist artist;
        Album actualAlbum;
        if (album != null)
        {
            actualAlbum = album;
            artist = album.Artist!;
        }
        else
        {
            artist = CreateArtist($"{title} Artist", ownerId);
            actualAlbum = CreateAlbum($"{title} Album", artist, ownerId);
        }

        var song = new Song
        {
            Title = title,
            Label = title,
            OwnerId = ownerId.Value,
            Owner = owner,
            AlbumId = actualAlbum.Id,
            Album = actualAlbum,
            Duration = TimeSpan.FromSeconds(180),
            Size = 5000000,
            RepositoryPath = repositoryPath ?? $"/music/{title}.mp3",
            Checksum = checksum ?? Guid.NewGuid().ToString(),
            ChecksumAlgorithm = checksumAlgorithm ?? "XxHash128",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = modifiedAt ?? DateTime.UtcNow,
            Year = year,
            Lyrics = lyrics,
            Bitrate = bitrate,
            CoverId = coverId,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        DbContext.Songs.Add(song);
        DbContext.SaveChanges();

        // Add SongArtist entries
        var artistList = artists ?? [artist];
        foreach (var a in artistList)
        {
            DbContext.Add(new SongArtist { SongId = song.Id, ArtistId = a.Id });
        }
        DbContext.SaveChanges();

        // Add SongGenre entries
        if (genres is { Count: > 0 })
        {
            foreach (var g in genres)
            {
                DbContext.Add(new SongGenre { SongId = song.Id, GenreId = g.Id });
            }
            DbContext.SaveChanges();
        }

        return song;
    }

    #endregion Seeding Data

    #region Sync Data

    public DeviceSyncSession CreateSession(
        Device device,
        SyncSessionStatus status = SyncSessionStatus.InProgress,
        bool isDryRun = false,
        string? repositoryPath = null,
        DateTime? startedAt = null)
    {
        var session = new DeviceSyncSession
        {
            DeviceId = device.Id,
            Device = device,
            StartedAt = startedAt ?? DateTime.UtcNow,
            Status = status,
            IsDryRun = isDryRun,
            RepositoryPath = repositoryPath,
            Records = [],
        };
        DbContext.DeviceSyncSessions.Add(session);
        DbContext.SaveChanges();
        return session;
    }

    public SongDevice CreateSongDevice(
        Device device,
        Song? song,
        string devicePath,
        DateTime? lastSyncedModifiedAt = null,
        SongSyncAction? syncAction = null)
    {
        var sd = new SongDevice
        {
            DeviceId = device.Id,
            Device = device,
            SongId = song?.Id,
            Song = song,
            DevicePath = devicePath,
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = lastSyncedModifiedAt,
            SyncAction = syncAction,
        };
        DbContext.Add(sd);
        DbContext.SaveChanges();
        return sd;
    }

    public DeviceSyncSessionRecord AddRecord(
        long sessionId,
        string filePath,
        SyncRecordAction action,
        JsonElement? data = null,
        long? songId = null,
        bool acknowledged = false)
    {
        var record = new DeviceSyncSessionRecord
        {
            SessionId = sessionId,
            FilePath = filePath,
            Action = action,
            Data = data,
            SongId = songId,
            Acknowledged = acknowledged,
            ProcessedAt = DateTime.UtcNow,
        };
        DbContext.DeviceSyncSessionRecords.Add(record);
        DbContext.SaveChanges();
        return record;
    }

    #endregion Sync Data

    #region Playlist Data

    public Playlist CreatePlaylist(
        string name,
        long? ownerId = null,
        PlaylistType type = PlaylistType.Playlist,
        long? currentSongId = null)
    {
        ownerId ??= AdminUser.Id;
        var owner = DbContext.Users.First(u => u.Id == ownerId);
        var playlist = new Playlist
        {
            Name = name,
            OwnerId = ownerId.Value,
            Owner = owner,
            Type = type,
            CurrentSongId = currentSongId,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            PlaylistSongs = [],
        };
        DbContext.Add(playlist);
        DbContext.SaveChanges();
        return playlist;
    }

    public PlaylistSong AddSongToPlaylist(
        Playlist playlist,
        Song song,
        double order,
        bool skipNextPlayback = false,
        bool stopAfterPlayback = false)
    {
        var playlistSong = new PlaylistSong
        {
            PlaylistId = playlist.Id,
            Playlist = playlist,
            SongId = song.Id,
            Song = song,
            Order = order,
            SkipNextPlayback = skipNextPlayback,
            StopAfterPlayback = stopAfterPlayback,
        };
        DbContext.Add(playlistSong);
        DbContext.SaveChanges();
        return playlistSong;
    }

    #endregion Playlist Data

    #region Static Methods

    public static MusicDbContext CreateDbContext()
    {
        var keepAliveConnection = new SqliteConnection("DataSource=:memory:");
        keepAliveConnection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(keepAliveConnection)
            .UseProjectables()
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .LogTo(Console.WriteLine)
            .Options;
        var context = new MusicDbContext(options);
        context.Database.EnsureCreated();
        context.SaveChanges();
        return context;
    }

    public static IFormFile CreateMockFormFile(byte[] content, string fileName = "song.mp3")
    {
        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns(fileName);
        formFile.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var stream = call.ArgAt<Stream>(0);
                await stream.WriteAsync(content);
            });
        formFile.OpenReadStream().Returns(new MemoryStream(content));
        return formFile;
    }

    public static IFileSystem CreateFileSystem() => new MockFileSystem();

    public MusicService CreateMusicService() =>
        new(FileSystem, Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
        }), Substitute.For<ISongMergeService>(), Substitute.For<ILogger<MusicService>>());

    public SeedService CreateSeedService(string? seedPath = null) =>
        new(FileSystem, DbContext, Options.Create(new Config 
        { 
            MusicRepositoryPath = "/data",
            SeedPath = seedPath,
        }), Substitute.For<ILogger<SeedService>>());

    #endregion Static Methods
}