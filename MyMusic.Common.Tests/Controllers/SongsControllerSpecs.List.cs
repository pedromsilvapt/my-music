using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Services;
using MyMusic.Common.Services.Songs;
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
            Substitute.For<ISongDevicesUpdateService>(),
            Substitute.For<ISongDevicePathPreviewService>(),
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

    [Fact]
    public async Task List_FilterBySharingName_IgnoresPlaylistsNotOwnedBySongOwner()
    {
        // Arrange — I share "Shared Song" with Carol through my own playlist; Bob adds
        // "Reshared Song" to his own playlist and shares it with Carol, which grants no access
        var scenario = new Scenario();
        var bob = scenario.CreateUser("Bob", "bob");
        var carol = scenario.CreateUser("Carol", "carol");
        var sharedSong = scenario.CreateSong("Shared Song");
        var resharedSong = scenario.CreateSong("Reshared Song");
        Share(sharedSong, carol, scenario.DbContext);

        var bobPlaylist = scenario.CreatePlaylist("Bob's Playlist", ownerId: bob.Id);
        scenario.AddSongToPlaylist(bobPlaylist, resharedSong, 1000.0);
        SharingTestHelpers.SharePlaylist(scenario.DbContext, bobPlaylist, carol);

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None,
            filter: @"sharing.name = ""Carol""");

        // Assert — only the song Carol can actually access is reported as shared with her
        response.Songs.Select(s => s.Title).ShouldBe(["Shared Song"]);
    }

    [Theory]
    [InlineData(@"device.path startsWith ""Dup/""", new[] { "Twice On Phone" })]
    [InlineData("device.copies > 1", new[] { "Twice On Phone", "Twice On Tablet" })]
    [InlineData(@"device.name = ""Phone"" and device.copies > 1", new[] { "Twice On Phone", "Twice On Tablet" })]
    [InlineData(@"device(name = ""Phone"" and copies > 1)", new[] { "Twice On Phone" })]
    [InlineData(@"device(name = ""Tablet"" and (copies > 1 or path startsWith ""Solo/""))", new[] { "Twice On Tablet" })]
    [InlineData("device[all](copies = 1)", new[] { "Nowhere", "Once On Phone" })]
    public async Task List_FilterByDevicePathAndCopies_ReturnsMatchingSongs(string filter, string[] expectedTitles)
    {
        // Arrange — one song is twice on the Phone, another is once on the Phone and twice on the Tablet
        var scenario = new Scenario();
        var phone = scenario.CreateDevice("Phone");
        var tablet = scenario.CreateDevice("Tablet");

        var twiceOnPhone = scenario.CreateSong("Twice On Phone");
        scenario.CreateSongDevice(phone, twiceOnPhone, "Music/Twice On Phone.mp3");
        scenario.CreateSongDevice(phone, twiceOnPhone, "Dup/Twice On Phone.mp3");

        var twiceOnTablet = scenario.CreateSong("Twice On Tablet");
        scenario.CreateSongDevice(phone, twiceOnTablet, "Music/Twice On Tablet.mp3");
        scenario.CreateSongDevice(tablet, twiceOnTablet, "Music/Twice On Tablet.mp3");
        scenario.CreateSongDevice(tablet, twiceOnTablet, "Copies/Twice On Tablet.mp3");

        var onceOnPhone = scenario.CreateSong("Once On Phone");
        scenario.CreateSongDevice(phone, onceOnPhone, "Solo/Once On Phone.mp3");

        scenario.CreateSong("Nowhere");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, filter: filter);

        // Assert
        response.Songs.Select(s => s.Title).ShouldBe(expectedTitles);
    }

    [Fact]
    public async Task List_FilterByDeviceCopies_IgnoresCopiesMarkedForRemoval()
    {
        // Arrange — the song is twice on the Phone, but one of the copies is to be removed by the next sync
        var scenario = new Scenario();
        var phone = scenario.CreateDevice("Phone");
        var song = scenario.CreateSong("Removed Copy");
        scenario.CreateSongDevice(phone, song, "Music/Removed Copy.mp3");
        scenario.CreateSongDevice(phone, song, "Dup/Removed Copy.mp3", syncAction: SongSyncAction.Remove);

        var controller = CreateController(scenario);

        // Act
        var duplicated = await controller.List(scenario.DbContext, CancellationToken.None,
            filter: @"device(name = ""Phone"" and copies > 1)");
        var single = await controller.List(scenario.DbContext, CancellationToken.None,
            filter: "device[all](copies = 1)");

        // Assert
        duplicated.Songs.ShouldBeEmpty();
        single.Songs.Select(s => s.Title).ShouldBe(["Removed Copy"]);
    }

    [Fact]
    public async Task List_SearchMatchesOnlyLyrics_SearchLyricsFalse_ReturnsNoSongs()
    {
        // Arrange — the searched words only exist in the song's lyrics
        var scenario = new Scenario();
        scenario.CreateSong("Hello", lyrics: "Hello from the other side");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None, search: "other side");

        // Assert — lyrics are not searched unless asked for
        response.Songs.ShouldBeEmpty();
    }

    [Fact]
    public async Task List_SearchMatchesOnlyLyrics_SearchLyricsTrue_ReturnsSong()
    {
        // Arrange — the searched words only exist in one song's lyrics
        var scenario = new Scenario();
        scenario.CreateSong("Hello", lyrics: "Hello from the Other Side");
        scenario.CreateSong("Numb", lyrics: "I've become so numb");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None,
            search: "other side", searchLyrics: true);

        // Assert — the song is found by its lyrics, ignoring case
        response.Songs.Select(s => s.Title).ShouldBe(["Hello"]);
    }

    [Fact]
    public async Task List_SearchTermsSplitAcrossTitleAndLyrics_SearchLyricsTrue_ReturnsSong()
    {
        // Arrange — "skyfall" is only in the title, "crumbles" only in the lyrics
        var scenario = new Scenario();
        scenario.CreateSong("Skyfall", lyrics: "Let the sky fall, when it crumbles");
        scenario.CreateSong("Hello", lyrics: "Hello from the other side");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None,
            search: "skyfall crumbles", searchLyrics: true);

        // Assert — each term may match either the usual fields or the lyrics
        response.Songs.Select(s => s.Title).ShouldBe(["Skyfall"]);
    }

    [Fact]
    public async Task List_SongWithoutLyrics_SearchLyricsTrue_StillMatchesByTitle()
    {
        // Arrange — a song with no lyrics at all
        var scenario = new Scenario();
        scenario.CreateSong("Instrumental Piece");

        var controller = CreateController(scenario);

        // Act
        var response = await controller.List(scenario.DbContext, CancellationToken.None,
            search: "instrumental", searchLyrics: true);

        // Assert — missing lyrics must not exclude the song from the usual matches
        response.Songs.Select(s => s.Title).ShouldBe(["Instrumental Piece"]);
    }
}
