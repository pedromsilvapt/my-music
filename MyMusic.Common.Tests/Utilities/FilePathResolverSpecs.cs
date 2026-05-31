using MyMusic.Common.Entities;
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
        return scenario.CreateSong("Song", ownerId: ownerId, repositoryPath: repositoryPath, checksum: "test-checksum-" + Guid.NewGuid());
    }
}