using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Tests.Services.Songs;
using MyMusic.Common.Sources;
using MyMusic.Server;
using MyMusic.Server.Controllers;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public class SongsControllerGetSpecs
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

    private static PlaylistSharing Share(Song song, User recipient, MusicDbContext db) =>
        SharingTestHelpers.ShareSongs(db, recipient, song);

    [Fact]
    public async Task Get_OwnedSong_Returns200()
    {
        // Arrange — current user owns the song
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var song = scenario.CreateSong("My Song");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(song.Id, scenario.DbContext, new SongHistoryPendingService(scenario.DbContext), CancellationToken.None);

        // Assert — owned song resolves normally
        response.Song.Id.ShouldBe(song.Id);
        response.Song.Title.ShouldBe("My Song");
    }

    [Fact]
    public async Task Get_SharedSong_Returns200()
    {
        // Arrange — another user owns the song but has shared it with me (read access)
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(song, me, scenario.DbContext);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(song.Id, scenario.DbContext, new SongHistoryPendingService(scenario.DbContext), CancellationToken.None);

        // Assert — recipient can read the shared song (WhereAccessibleBy gate)
        response.Song.Id.ShouldBe(song.Id);
        response.Song.Title.ShouldBe("Shared Song");
    }

    [Fact]
    public async Task Get_OwnedSongWithQueuedHistory_ReportsPendingHistory()
    {
        // Arrange — an edit to my song is still waiting for the history worker
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");
        SongHistoryPendingServiceSpecs.AddQueueEntry(scenario, song.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(song.Id, scenario.DbContext, new SongHistoryPendingService(scenario.DbContext), CancellationToken.None);

        // Assert
        response.Song.HasPendingHistory.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_OwnedSongWithoutQueuedHistory_ReportsNoPendingHistory()
    {
        // Arrange
        var scenario = new Scenario();
        var song = scenario.CreateSong("My Song");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(song.Id, scenario.DbContext, new SongHistoryPendingService(scenario.DbContext), CancellationToken.None);

        // Assert
        response.Song.HasPendingHistory.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_SharedSongWithQueuedHistory_ReportsNoPendingHistory()
    {
        // Arrange — history is owner-only, so a recipient never sees pending history
        var scenario = new Scenario();
        var me = scenario.AdminUser;
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Shared Song", ownerId: other.Id);
        Share(song, me, scenario.DbContext);
        SongHistoryPendingServiceSpecs.AddQueueEntry(scenario, song.Id);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.Get(song.Id, scenario.DbContext, new SongHistoryPendingService(scenario.DbContext), CancellationToken.None);

        // Assert
        response.Song.HasPendingHistory.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_OtherUserNonSharedSong_Returns404()
    {
        // Arrange — another user owns the song and has NOT shared it with me
        var scenario = new Scenario();
        var other = scenario.CreateUser("Other", "other");
        var song = scenario.CreateSong("Private Other Song", ownerId: other.Id);

        var controller = CreateController(scenario);

        // Act & Assert — access gate rejects the recipient; controller throws (404 in pipeline)
        await Should.ThrowAsync<Exception>(() =>
            controller.Get(song.Id, scenario.DbContext, new SongHistoryPendingService(scenario.DbContext), CancellationToken.None));
    }
}