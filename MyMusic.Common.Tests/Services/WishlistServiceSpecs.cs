using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Sources;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class WishlistServiceSpecs
{
    [Fact]
    public async Task CreateWishlistItem_ValidData_CreatesItemWithCorrectHash()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        var sourcesService = Substitute.For<ISourcesService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
            WishlistMaxResultsToHash = 50
        });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        var songIds = new List<string> { "song1", "song2", "song3" };

        // Act
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            songIds,
            CancellationToken.None);

        // Assert
        item.ShouldNotBeNull();
        item.SourceId.ShouldBe(source.Id);
        item.Query.ShouldBe("test query");
        item.Status.ShouldBe(WishlistItemStatus.Active);
        item.Hash.ShouldNotBeNullOrEmpty();
        
        var savedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        savedItem.ShouldNotBeNull();
        savedItem.Hash.ShouldBe(item.Hash);
    }

    [Fact]
    public async Task CreateWishlistItem_DuplicateQuery_ReturnsExistingItem()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        var sourcesService = Substitute.For<ISourcesService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        var songIds = new List<string> { "song1", "song2" };

        // Act - Create first item
        var item1 = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            songIds,
            CancellationToken.None);

        // Act - Try to create duplicate
        var item2 = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            songIds,
            CancellationToken.None);

        // Assert
        item2.Id.ShouldBe(item1.Id);
        
        var count = await scenario.DbContext.WishlistItems.CountAsync();
        count.ShouldBe(1);
    }

    [Fact]
    public async Task CheckForUpdates_HashChanged_MarksAsUpdated()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var mockSource = Substitute.For<ISource>();
        mockSource.SearchSongsAsync("test query", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SourceSong>
            {
                new()
                {
                    Id = "new-song-1",
                    Title = "New Song 1",
                    Artists = [],
                    Genres = [],
                    Album = new SourceSongAlbum
                    {
                        Id = "album-1", Name = "Album", Artist = new SourceSongArtist { Id = "artist-1", Name = "Artist" }
                    }
                },
                new()
                {
                    Id = "new-song-2",
                    Title = "New Song 2",
                    Artists = [],
                    Genres = [],
                    Album = new SourceSongAlbum
                    {
                        Id = "album-1", Name = "Album", Artist = new SourceSongArtist { Id = "artist-1", Name = "Artist" }
                    }
                }
            }));
        
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockSource));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        // Create initial item
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            new List<string> { "old-song-1", "old-song-2" },
            CancellationToken.None);

        // Act
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.Status.ShouldBe(WishlistItemStatus.Updated);
    }

    [Fact]
    public async Task CheckForUpdates_HashUnchanged_KeepsActive()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var songIds = new List<string> { "song-1", "song-2" };
        var mockSource = Substitute.For<ISource>();
        mockSource.SearchSongsAsync("test query", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(songIds.Select(id => new SourceSong
            {
                Id = id,
                Title = $"Song {id}",
                Artists = [],
                Genres = [],
                Album = new SourceSongAlbum
                {
                    Id = "album-1", Name = "Album", Artist = new SourceSongArtist { Id = "artist-1", Name = "Artist" }
                }
            }).ToList()));
        
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockSource));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        // Create item with same song IDs as mock will return
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            songIds,
            CancellationToken.None);

        // Act
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.Status.ShouldBe(WishlistItemStatus.Active);
    }

    [Fact]
    public async Task UpdateWishlistItem_ResetsHashAndStatus()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var mockSource = Substitute.For<ISource>();
        mockSource.SearchSongsAsync("test query", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SourceSong>
            {
                new()
                {
                    Id = "updated-song",
                    Title = "Updated Song",
                    Artists = [],
                    Genres = [],
                    Album = new SourceSongAlbum
                    {
                        Id = "album-1", Name = "Album", Artist = new SourceSongArtist { Id = "artist-1", Name = "Artist" }
                    }
                }
            }));
        
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockSource));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        // Create item and mark as Updated
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            new List<string> { "old-song" },
            CancellationToken.None);
        
        var originalHash = item.Hash;
        item.Status = WishlistItemStatus.Updated;
        await scenario.DbContext.SaveChangesAsync();

        // Act
        var updatedItem = await wishlistService.UpdateHashAsync(item.Id, CancellationToken.None);

        // Assert
        updatedItem.Status.ShouldBe(WishlistItemStatus.Active);
        updatedItem.Hash.ShouldNotBe(originalHash);
    }

    [Fact]
    public async Task DeleteWishlistItem_RemovesFromDatabase()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        var sourcesService = Substitute.For<ISourcesService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            new List<string> { "song1" },
            CancellationToken.None);

        // Act
        await wishlistService.DeleteAsync(item.Id, CancellationToken.None);

        // Assert
        var deletedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        deletedItem.ShouldBeNull();
    }

    [Fact]
    public async Task CheckForUpdates_SourceNotFound_IncrementsFailureCountAndSetsErrorMessage()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ISource>(new Exception("Source not found")));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            new List<string> { "song1" },
            CancellationToken.None);

        var originalUpdatedAt = item.UpdatedAt;

        // Act
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.ContinuousFailedCount.ShouldBe(1);
        updatedItem.LastErrorMessage.ShouldBe("Source not found");
        updatedItem.UpdatedAt.ShouldBe(originalUpdatedAt); // Should NOT be updated on failure
        updatedItem.Status.ShouldBe(WishlistItemStatus.Active);
    }

    [Fact]
    public async Task CheckForUpdates_ConsecutiveFailures_IncrementsFailureCount()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ISource>(new Exception("Connection timeout")));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            new List<string> { "song1" },
            CancellationToken.None);

        // Act - First failure
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);
        
        // Act - Second failure
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.ContinuousFailedCount.ShouldBe(2);
        updatedItem.LastErrorMessage.ShouldBe("Connection timeout");
    }

    [Fact]
    public async Task CheckForUpdates_AfterFailureThenSuccess_ResetsFailureTracking()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var songIds = new List<string> { "song-1", "song-2" };
        var mockSource = Substitute.For<ISource>();
        mockSource.SearchSongsAsync("test query", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(songIds.Select(id => new SourceSong
            {
                Id = id,
                Title = $"Song {id}",
                Artists = [],
                Genres = [],
                Album = new SourceSongAlbum
                {
                    Id = "album-1", Name = "Album", Artist = new SourceSongArtist { Id = "artist-1", Name = "Artist" }
                }
            }).ToList()));
        
        var sourcesService = Substitute.For<ISourcesService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            songIds,
            CancellationToken.None);

        // Pre-set failure state
        item.ContinuousFailedCount = 3;
        item.LastErrorMessage = "Previous error";
        await scenario.DbContext.SaveChangesAsync();

        // Setup success mock for second call
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockSource));

        // Act
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.ContinuousFailedCount.ShouldBe(0);
        updatedItem.LastErrorMessage.ShouldBeNull();
        updatedItem.Status.ShouldBe(WishlistItemStatus.Active);
    }

    [Fact]
    public async Task CheckForUpdates_LongErrorMessage_TruncatesTo1024Chars()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var longErrorMessage = new string('A', 2000);
        var sourcesService = Substitute.For<ISourcesService>();
        sourcesService.GetSourceClientAsync(source.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ISource>(new Exception(longErrorMessage)));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            new List<string> { "song1" },
            CancellationToken.None);

        // Act
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.LastErrorMessage.ShouldNotBeNull();
        updatedItem.LastErrorMessage.Length.ShouldBe(1024);
        updatedItem.LastErrorMessage.ShouldBe(new string('A', 1024));
    }

    private static Source CreateSource(MusicDbContext db)
    {
        var source = new Source
        {
            Name = "Test Source",
            Icon = "test-icon",
            Address = "http://test.com",
            IsPaid = false
        };
        
        db.Sources.Add(source);
        db.SaveChanges();
        
        return source;
    }
}