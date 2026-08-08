using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Services;
using MyMusic.Common.Sources;
using MyMusic.Server;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SongsControllerSpecs
{
    private static SongsController CreateController(Scenario scenario, long? currentUserId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(currentUserId ?? scenario.AdminUser.Id);

        var config = Microsoft.Extensions.Options.Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
        });
        var serverConfig = Microsoft.Extensions.Options.Options.Create(new ServerConfig
        {
            ClientUrl = "http://localhost",
        });

        return new SongsController(
            Substitute.For<ILogger<SongsController>>(),
            currentUser,
            config,
            serverConfig,
            Substitute.For<ISongUpdateService>(),
            Substitute.For<ISongDeleteService>(),
            Substitute.For<IMusicService>(),
            scenario.FileSystem,
            Substitute.For<ILogger<MusicImportJob>>(),
            Substitute.For<ISourcesService>(),
            Substitute.For<IAuditService>(),
            Substitute.For<IThumbnailProxyService>(),
            Substitute.For<IImageComparisonService>(),
            new MetadataDiffBuilder(Substitute.For<IApiPathResolver>()));
    }

    private static SongSharing Share(Song song, User recipient, MusicDbContext db)
    {
        var sharing = new SongSharing
        {
            SongId = song.Id,
            UserId = recipient.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.SongSharings.Add(sharing);
        db.SaveChanges();
        return sharing;
    }

    [Fact]
    public async Task List_NoOwnerId_ReturnsOwnSongs()
    {
        // Arrange — current user owns two songs; another user owns one (not shared)
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("My Song A");
        scenario.CreateSong("My Song B");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None);

        // Assert — only the current user's own songs are returned
        var titles = response.Songs.Select(s => s.Title).ToList();
        titles.Count.ShouldBe(2);
        titles.ShouldContain("My Song A");
        titles.ShouldContain("My Song B");
        titles.ShouldNotContain("Other Song");
    }

    [Fact]
    public async Task List_OwnerIdEqualsSelf_ReturnsOwnSongs()
    {
        // Arrange — explicit ownerId == self behaves like the default "my library" view
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("My Song");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: me.Id);

        // Assert — only the current user's own songs are returned
        var titles = response.Songs.Select(s => s.Title).ToList();
        titles.ShouldBe(["My Song"]);
    }

    [Fact]
    public async Task List_OwnerIdIsOtherUser_WithShare_ReturnsSharedSongs()
    {
        // Arrange — another user owns two songs; only one is shared with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id);
        var unsharedSong = scenario.CreateSong("Unshared Song", ownerId: other.Id);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — only the shared song is returned (gate-by-sharing semantics)
        var titles = response.Songs.Select(s => s.Title).ToList();
        titles.ShouldBe(["Shared Song"]);
    }

    [Fact]
    public async Task List_OwnerIdIsOtherUser_WithoutShare_ReturnsEmpty()
    {
        // Arrange — another user owns a song but has not shared it with me
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        scenario.CreateSong("Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — nothing is visible (no share row gates the recipient in)
        response.Songs.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_DTO_IsShared_TrueForOtherOwnerSongs()
    {
        // Arrange — a shared song surfaces with IsShared = true (drives the client Import affordance)
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var sharedSong = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(sharedSong, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, ownerId: other.Id);

        // Assert — the shared song is flagged IsShared (recipient view)
        var item = response.Songs.Single();
        item.IsShared.ShouldBeTrue();
    }

    [Fact]
    public async Task List_DTO_IsShared_FalseForOwnSongs()
    {
        // Arrange — own library view: IsShared must be false on every song
        var scenario = new Scenario();
        scenario.CreateSong("My Song");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None);

        // Assert — owned songs are never flagged as shared
        var item = response.Songs.Single();
        item.IsShared.ShouldBeFalse();
    }
}