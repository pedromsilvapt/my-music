using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Utilities;

/// <summary>
///     Songs backed by real files in a <see cref="Scenario"/>, for the operations that rewrite many songs through
///     <see cref="ISongUpdateService.UpdateSongsAsync"/>.
/// </summary>
public static class SongFileScenarioExtensions
{
    public static SongFileUpdateService CreateSongFileUpdateService(this Scenario scenario) =>
        new(scenario.FileSystem, Options.Create(new Config { MusicRepositoryPath = "/data" }));

    public static SongUpdateService CreateSongUpdateService(this Scenario scenario,
        ISongFileUpdateService? songFileUpdate = null) =>
        new(songFileUpdate ?? scenario.CreateSongFileUpdateService(),
            scenario.FileTransactions,
            scenario.AdvisoryLocks,
            new AlbumUpsertService(),
            new AlbumDeleteService(scenario.DbContext, Substitute.For<ILogger<AlbumDeleteService>>()),
            new ArtistDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtistDeleteService>>()),
            new ArtworkDeleteService(scenario.DbContext, Substitute.For<ILogger<ArtworkDeleteService>>()),
            Substitute.For<ILogger<SongUpdateService>>());

    /// <summary>
    ///     A song whose file already carries the song's metadata and sits at the path it generates, on
    ///     <paramref name="device"/> with nothing pending: whatever an operation changes from here is caused by that
    ///     operation alone.
    /// </summary>
    public static async Task<Song> CreateSyncedSongAsync(this Scenario scenario, string title, Album album,
        Device device, List<Artist>? artists = null)
    {
        var path = $"/data/{title}.mp3";
        MockMusicFile.Create(scenario.FileSystem, path, title, album.Name, [album.Artist.Name], ["Rock"]);
        var algorithm = new XxHash128();
        var checksum = ChecksumService.CalculateChecksum(scenario.FileSystem, algorithm, path);

        var song = scenario.CreateSong(title, checksum: checksum, checksumAlgorithm: algorithm.GetType().Name,
            repositoryPath: path, album: album, artists: artists);
        await scenario.CreateSongUpdateService().UpdateSong(scenario.DbContext, song.Id, new SongUpdateModel());

        scenario.DbContext.Add(new SongDevice
        {
            SongId = song.Id,
            DeviceId = device.Id,
            DevicePath = $"/music/{title}.mp3",
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = DateTime.UtcNow,
        });
        scenario.DbContext.SaveChanges();

        return scenario.LoadSong(song.Id);
    }

    /// <summary>The saved song, untracked, with its album, album artist and artists (in order).</summary>
    public static Song LoadSong(this Scenario scenario, long songId)
    {
        var song = scenario.DbContext.Songs
            .AsNoTracking()
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(s => s.Devices)
            .First(s => s.Id == songId);
        song.Artists = song.Artists.OrderBy(sa => sa.Id).ToList();

        return song;
    }

    public static Dictionary<string, byte[]> ReadMusicFiles(this Scenario scenario) =>
        scenario.FileSystem.Directory.GetFiles("/data", "*.mp3", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => scenario.FileSystem.File.ReadAllBytes(path));

    /// <summary>Asserts the songs, their devices and the music files are as they were before a failed operation.</summary>
    public static void ShouldHaveUnchangedSongs(this Scenario scenario, IEnumerable<Song> songsBefore,
        Dictionary<string, byte[]> filesBefore)
    {
        scenario.DbContext.ChangeTracker.Clear();
        scenario.DbContext.Database.CurrentTransaction.ShouldBeNull();

        foreach (var before in songsBefore)
        {
            var song = scenario.LoadSong(before.Id);
            song.AlbumId.ShouldBe(before.AlbumId);
            song.Artists.Select(sa => sa.ArtistId).ShouldBe(before.Artists.Select(sa => sa.ArtistId));
            song.Label.ShouldBe(before.Label);
            song.RepositoryPath.ShouldBe(before.RepositoryPath);
            song.Checksum.ShouldBe(before.Checksum);
            song.FileModifiedAt.ShouldBe(before.FileModifiedAt);
            song.Devices.ShouldAllBe(sd => sd.SyncAction == null);
        }

        var files = scenario.ReadMusicFiles();
        files.Keys.ShouldBe(filesBefore.Keys, ignoreOrder: true);
        foreach (var (path, content) in filesBefore)
        {
            files[path].ShouldBe(content);
        }
    }
}

/// <summary>Fails the file update of one song, as a full disk or a corrupt file would.</summary>
public sealed class FailingSongFileUpdateService(ISongFileUpdateService inner, long failingSongId)
    : ISongFileUpdateService
{
    public Task<SongFileUpdateResult> UpdateAsync(MusicDbContext db, IFileTransaction files, Song song,
        Func<string> downloadReason, CancellationToken cancellationToken = default) =>
        song.Id == failingSongId
            ? throw new IOException($"Cannot write the file of song {song.Id}")
            : inner.UpdateAsync(db, files, song, downloadReason, cancellationToken);
}
