using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Services;
using MyMusic.Common.Tests.Utilities;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class MusicServiceSpecs
{
    [Fact]
    public async Task ImportMusic_EmptyDatabase()
    {
        // Arrange
        var scenario = new Scenario();
        var musicService = scenario.CreateMusicService();
        var job = new MusicImportJob(Substitute.For<ILogger>());
        
        MockMusicFile.Create(scenario.FileSystem, "/music/Title A.mp3", "Title A", "Album A", ["Artist A", "Artist B"], ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title B.mp3", "Title B", "Album A", ["Artist A"], ["Genre A", "Genre B"]);
        MockMusicFile.Create(scenario.FileSystem, "/music/Title C.mp3", "Title C", "Album B", ["Artist C"], ["Genre B"]);

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