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

public class SongsControllerFilterValuesSpecs
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
            Song = song,
            User = recipient,
            CreatedAt = DateTime.UtcNow,
        };
        db.SongSharings.Add(sharing);
        db.SaveChanges();
        return sharing;
    }

    [Fact]
    public async Task GetFilterValues_SharingName_ReturnsRecipientsOfOwnedSongs()
    {
        // Arrange — I own songs and have shared them with alice and bob (autocomplete is owner-scoped)
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var alice = scenario.CreateUser("Alice", "alice");
        var bob = scenario.CreateUser("Bob", "bob");
        var mySongA = scenario.CreateSong("My Song A");
        var mySongB = scenario.CreateSong("My Song B");
        Share(mySongA, alice, scenario.DbContext);
        Share(mySongB, bob, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.GetFilterValues(
            "sharing.name", scenario.DbContext, CancellationToken.None);

        // Assert — distinct recipient names of my owned songs are surfaced
        response.Values.ShouldContain("Alice");
        response.Values.ShouldContain("Bob");
        response.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetFilterValues_SharingName_OwnerScoped_ExcludesSharesByOthers()
    {
        // Arrange — another user owns a song and shares it with carol; I share my song with alice
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var alice = scenario.CreateUser("Alice", "alice");
        var carol = scenario.CreateUser("Carol", "carol");
        var mySong = scenario.CreateSong("My Song");
        var otherSong = scenario.CreateSong("Other Song", ownerId: other.Id);
        Share(mySong, alice, scenario.DbContext);
        Share(otherSong, carol, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.GetFilterValues(
            "sharing.name", scenario.DbContext, CancellationToken.None);

        // Assert — only recipients of MY songs appear (carol is excluded — owner-scoped)
        response.Values.ShouldBe(["Alice"]);
    }

    [Fact]
    public async Task GetFilterValues_SharingName_SearchFiltersRecipients()
    {
        // Arrange — I shared with alice and alicia; the search term narrows the autocomplete
        var scenario = new Scenario();
        var alice = scenario.CreateUser("Alice", "alice");
        var alicia = scenario.CreateUser("Alicia", "alicia");
        var bob = scenario.CreateUser("Bob", "bob");
        var mySong = scenario.CreateSong("My Song");
        Share(mySong, alice, scenario.DbContext);
        Share(mySong, alicia, scenario.DbContext);
        Share(mySong, bob, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.GetFilterValues(
            "sharing.name", scenario.DbContext, CancellationToken.None, search: "ali");

        // Assert — only recipients whose name contains "ali" remain
        response.Values.ShouldContain("Alice");
        response.Values.ShouldContain("Alicia");
        response.Values.ShouldNotContain("Bob");
        response.Values.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetFilterValues_SharingName_DistinctRecipients()
    {
        // Arrange — I shared two different songs with the same recipient (alice)
        var scenario = new Scenario();
        var alice = scenario.CreateUser("Alice", "alice");
        var mySongA = scenario.CreateSong("My Song A");
        var mySongB = scenario.CreateSong("My Song B");
        Share(mySongA, alice, scenario.DbContext);
        Share(mySongB, alice, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.GetFilterValues(
            "sharing.name", scenario.DbContext, CancellationToken.None);

        // Assert — alice appears once even though she was shared two songs
        response.Values.ShouldBe(["Alice"]);
    }

    [Fact]
    public async Task GetFilterValues_SharingName_NoShares_ReturnsEmpty()
    {
        // Arrange — I own songs but haven't shared any
        var scenario = new Scenario();
        scenario.CreateSong("My Song");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.GetFilterValues(
            "sharing.name", scenario.DbContext, CancellationToken.None);

        // Assert — no recipients to surface
        response.Values.ShouldBeEmpty();
    }
}