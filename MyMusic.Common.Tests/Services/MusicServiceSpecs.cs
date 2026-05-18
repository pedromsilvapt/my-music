using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class MusicServiceSpecs
{
    [Fact]
    public async Task ImportMusic_EmptyDatabase_CreatesSongs()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Title A.mp3", "Title A", "Album A", ["Artist A", "Artist B"],
            ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title B.mp3", "Title B", "Album A", ["Artist A"],
            ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title C.mp3", "Title C", "Album B", ["Artist C"],
            ["Genre B"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        job.SkipReasons.ShouldBeEmpty();

        // Songs
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(3);
        songs[0].ShouldSatisfyAllConditions(
            s => s.Title.ShouldBe("Title A"),
            s => s.Album.ShouldNotBeNull(),
            s => s.Album!.Name.ShouldBe("Album A"),
            s => s.Artists.Select(a => a.Artist.Name).ShouldBe(["Artist A", "Artist B"], ignoreOrder: true),
            s => s.Genres.Select(g => g.Genre.Name).ShouldBe(["Genre A", "Genre B"], ignoreOrder: true)
        );
        songs[1].ShouldSatisfyAllConditions(
            s => s.Title.ShouldBe("Title B"),
            s => s.Album.ShouldNotBeNull(),
            s => s.Album!.Name.ShouldBe("Album A"),
            s => s.Artists.Select(a => a.Artist.Name).ShouldBe(["Artist A"], ignoreOrder: true),
            s => s.Genres.Select(g => g.Genre.Name).ShouldBe(["Genre A", "Genre B"], ignoreOrder: true)
        );
        songs[2].ShouldSatisfyAllConditions(
            s => s.Title.ShouldBe("Title C"),
            s => s.Album.ShouldNotBeNull(),
            s => s.Album!.Name.ShouldBe("Album B"),
            s => s.Artists.Select(a => a.Artist.Name).ShouldBe(["Artist C"], ignoreOrder: true),
            s => s.Genres.Select(g => g.Genre.Name).ShouldBe(["Genre B"], ignoreOrder: true)
        );

        // Albums
        scenario.DbContext.Albums
            .Select(a => a.Name)
            .ToList()
            .ShouldBe(["Album A", "Album B"], ignoreOrder: true);

        // Artists
        scenario.DbContext.Artists
            .Select(a => a.Name)
            .ToList()
            .ShouldBe(["Artist A", "Artist B", "Artist C"], ignoreOrder: true);

        // Genres
        scenario.DbContext.Genres
            .Select(a => a.Name)
            .ToList()
            .ShouldBe(["Genre A", "Genre B"], ignoreOrder: true);
    }

    [Fact]
    public async Task ImportMusic_ExtractsAndStoresBitrate()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "Album", ["Artist"], ["Genre"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(1);
        songs[0].Bitrate.ShouldNotBeNull();
        songs[0].Bitrate!.Value.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ImportMusic_SamePathDifferentChecksum_ResolvesWithCounter()
    {
        // Two songs with identical metadata (same title/album/artist) but different file content
        // should both be stored, with the second getting a "(2)" suffix on its path
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song_v2.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        job.SkipReasons.ShouldBeEmpty();
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(2);

        var paths = songs.Select(s => s.RepositoryPath).ToList();
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist.mp3");
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist (2).mp3");
    }

    [Fact]
    public async Task ImportMusic_SamePathDifferentChecksum_ThreeSongs_ResolvesWithIncrementingCounters()
    {
        // Three songs with identical metadata but different content should get base, (2), and (3) paths
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song_v2.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);
        MockMusicFile.CreateWithDifferentContent(scenario.FileSystem, "/music/Song_v3.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        // Act
        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");

        // Assert
        job.SkipReasons.ShouldBeEmpty();
        var songs = LoadSongs(scenario.DbContext);
        songs.Count.ShouldBe(3);

        var paths = songs.Select(s => s.RepositoryPath).ToList();
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist.mp3");
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist (2).mp3");
        paths.ShouldContain("/data/admin/My Artist/My Album/Song - My Artist (3).mp3");
    }

    [Fact]
    public async Task ImportMusic_SameChecksum_SamePath_DoesNotCreateDuplicateSongs()
    {
        // Importing the same file twice should not create a duplicate song.
        // The second import either skips (if file exists on disk) or updates the existing song.
        // Either way, only one song should exist in the DB.
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        // Act - import once
        var job1 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job1, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job1.Exceptions.ShouldBeEmpty();

        var songsAfterFirst = LoadSongs(scenario.DbContext);
        songsAfterFirst.Count.ShouldBe(1);

        // Act - import the same directory again (same file, same checksum)
        var job2 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job2, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job2.Exceptions.ShouldBeEmpty();

        // Assert - still only one song (not duplicated, path collision resolved or updated)
        var songsAfterSecond = LoadSongs(scenario.DbContext);
        songsAfterSecond.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ImportMusic_EmptyDatabase_IncrementsArtistSongsCount()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());

        MockMusicFile.Create(scenario.FileSystem, "/music/Title A.mp3", "Title A", "Album A", ["Artist A", "Artist B"],
            ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title B.mp3", "Title B", "Album A", ["Artist A"],
            ["Genre A", "Genre B"]);

        await musicService.ImportRepositorySongs(scenario.DbContext, job, scenario.AdminUser.Id, "/music");
        job.Exceptions.ShouldBeEmpty();

        var artists = scenario.DbContext.Artists.AsNoTracking().ToList();
        var artistA = artists.First(a => a.Name == "Artist A");
        var artistB = artists.First(a => a.Name == "Artist B");

        artistA.SongsCount.ShouldBe(2);
        artistB.SongsCount.ShouldBe(1);
    }

    [Fact]
    public async Task ImportMusic_ReImportSameSongs_DoesNotDoubleIncrementSongsCount()
    {
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();

        MockMusicFile.Create(scenario.FileSystem, "/music/Song.mp3", "Song", "My Album", ["My Artist"], ["Rock"]);

        var job1 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job1, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job1.Exceptions.ShouldBeEmpty();

        var artistAfterFirst = scenario.DbContext.Artists.First(a => a.Name == "My Artist");
        var songsCountAfterFirst = artistAfterFirst.SongsCount;

        var job2 = new MusicImportJob(Substitute.For<ILogger<MusicImportJob>>());
        await musicService.ImportRepositorySongs(scenario.DbContext, job2, scenario.AdminUser.Id, "/music",
            duplicatesStrategy: DuplicateSongsHandlingStrategy.Skip);
        job2.Exceptions.ShouldBeEmpty();

        var artistAfterSecond = scenario.DbContext.Artists.First(a => a.Name == "My Artist");
        artistAfterSecond.SongsCount.ShouldBe(songsCountAfterFirst);
    }

    private static List<Song> LoadSongs(MusicDbContext context)
    {
        return context.Songs
            .OrderBy(s => s.Title)
            .Include(s => s.Artists)
            .Include(s => s.Genres)
            .Include(s => s.Cover)
            .Include(s => s.Album)
            .ThenInclude(a => a!.Artist)
            .AsSplitQuery()
            .ToList();
    }
}