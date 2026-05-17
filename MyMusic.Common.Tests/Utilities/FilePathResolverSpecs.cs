using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Tests;
using MyMusic.Common.Utilities;
using Shouldly;

namespace MyMusic.Common.Tests.Utilities;

public class FilePathResolverSpecs
{
    [Fact]
    public async Task ResolveConflict_NoCollision_ReturnsOriginalPath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe(basePath);
    }

    [Fact]
    public async Task ResolveConflict_OneCollision_ReturnsCounterTwoPath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe("/data/admin/Artist/Album/SongTitle (2).mp3");
    }

    [Fact]
    public async Task ResolveConflict_TwoCollisions_ReturnsCounterThreePath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, "/data/admin/Artist/Album/SongTitle (2).mp3");

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe("/data/admin/Artist/Album/SongTitle (3).mp3");
    }

    [Fact]
    public async Task ResolveConflict_WithExtension_ReturnsCorrectCounterPath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.flac";
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe("/data/admin/Artist/Album/SongTitle (2).flac");
    }

    [Fact]
    public async Task ResolveConflict_WithSubdirectories_ReturnsCorrectPath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe("/data/admin/Artist/Album/SongTitle (2).mp3");
    }

    [Fact]
    public async Task ResolveConflict_NoExtension_ReturnsCorrectCounterPath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle";
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe("/data/admin/Artist/Album/SongTitle (2)");
    }

    [Fact]
    public async Task ResolveConflict_WithExcludeSongId_ExcludesOwnPath()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";
        var song = CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, song.Id, scenario.DbContext);

        result.ShouldBe(basePath);
    }

    [Fact]
    public async Task ResolveConflict_WithExcludeSongId_OtherPathsStillCollide()
    {
        var scenario = new Scenario();
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";
        CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, basePath);
        var song2 = CreateSongWithRepositoryPath(scenario, scenario.AdminUser.Id, "/data/admin/Artist/Album/SongTitle (2).mp3");

        // Excluding song2 means its path "(2)" is considered free, so the resolver returns "(2)"
        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, song2.Id, scenario.DbContext);

        result.ShouldBe("/data/admin/Artist/Album/SongTitle (2).mp3");
    }

    [Fact]
    public async Task ResolveConflict_ScopedToUser()
    {
        var scenario = new Scenario();
        var otherUser = scenario.CreateUser("Other User", "otheruser");
        var basePath = "/data/admin/Artist/Album/SongTitle.mp3";
        CreateSongWithRepositoryPath(scenario, otherUser.Id, basePath);

        var result = await FilePathResolver.ResolveConflictAsync(
            basePath, scenario.AdminUser.Id, scenario.DbContext);

        result.ShouldBe(basePath);
    }

    private Song CreateSongWithRepositoryPath(Scenario scenario, long ownerId, string repositoryPath)
    {
        var user = scenario.DbContext.Users.First(u => u.Id == ownerId);
        var artist = new Artist
        {
            Name = $"Artist for {repositoryPath}",
            OwnerId = ownerId,
            Owner = user,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        scenario.DbContext.Add(artist);
        scenario.DbContext.SaveChanges();

        var album = new Album
        {
            Name = $"Album for {repositoryPath}",
            OwnerId = ownerId,
            Owner = user,
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        scenario.DbContext.Add(album);
        scenario.DbContext.SaveChanges();

        var song = new Song
        {
            Title = $"Song for {repositoryPath}",
            Label = $"Song for {repositoryPath}",
            OwnerId = ownerId,
            Owner = user,
            AlbumId = album.Id,
            Album = album,
            Duration = TimeSpan.FromSeconds(180),
            Size = 5000000,
            RepositoryPath = repositoryPath,
            Checksum = "test-checksum-" + Guid.NewGuid(),
            ChecksumAlgorithm = "XxHash128",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = [],
        };
        scenario.DbContext.Add(song);
        scenario.DbContext.SaveChanges();

        return song;
    }
}