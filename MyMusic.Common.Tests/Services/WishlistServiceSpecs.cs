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
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
            WishlistMaxResultsToHash = 50
        });
        
        // Mock the search service to return song IDs for initial hash computation
        var songIds = new List<string> { "song1", "song2", "song3" };
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(songIds));
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);

        // Act
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
            CancellationToken.None);

        // Assert
        item.ShouldNotBeNull();
        item.SourceId.ShouldBe(source.Id);
        item.Query.ShouldBe("test query");
        item.Filter.ShouldBeNull();
        item.Status.ShouldBe(WishlistItemStatus.Active);
        item.Hash.ShouldNotBeNullOrEmpty();
        
        var savedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        savedItem.ShouldNotBeNull();
        savedItem.Hash.ShouldBe(item.Hash);
        
        // Verify the search service was called to compute the initial hash
        await purchasesSearchService.Received().SearchForHashAsync(
            source.Id, 
            "test query", 
            null, 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWishlistItem_DuplicateQuery_ReturnsExistingItem()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        // Mock the search service to return song IDs for both creations
        var songIds = new List<string> { "song1", "song2" };
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(songIds));

        // Act - Create first item
        var item1 = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
            CancellationToken.None);

        // Act - Try to create duplicate
        var item2 = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        
        // Setup mock to return different values on consecutive calls:
        // First call (creation): initial song IDs
        // Second call (check): different song IDs to trigger update
        var initialSongIds = new List<string> { "initial-song-1", "initial-song-2" };
        var updatedSongIds = new List<string> { "new-song-1", "new-song-2" };
        var callCount = 0;
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return Task.FromResult(callCount == 1 ? initialSongIds : updatedSongIds);
            });
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        // Create initial item
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        // Mock returning the same song IDs as what was stored
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(songIds));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        // Create item with same song IDs as mock will return
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        
        // Setup mock to return different values on consecutive calls:
        // First call (creation): initial song IDs
        // Subsequent calls: different song IDs for update
        var initialSongIds = new List<string> { "initial-song" };
        var updatedSongIds = new List<string> { "updated-song" };
        var callCount = 0;
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return Task.FromResult(callCount == 1 ? initialSongIds : updatedSongIds);
            });
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        // Create item and mark as Updated
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data" });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        
        // First call (creation) succeeds, subsequent calls fail
        var callCount = 0;
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new List<string> { "song1" });
                return Task.FromException<List<string>>(new Exception("Source not found"));
            });
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        
        // First call (creation) succeeds, subsequent calls fail
        var callCount = 0;
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new List<string> { "song1" });
                return Task.FromException<List<string>>(new Exception("Connection timeout"));
            });
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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
        
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        // Mock successful search returning the same song IDs
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(songIds));
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
            CancellationToken.None);

        // Pre-set failure state
        item.ContinuousFailedCount = 3;
        item.LastErrorMessage = "Previous error";
        await scenario.DbContext.SaveChangesAsync();

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
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        
        // First call (creation) succeeds, subsequent calls fail
        var callCount = 0;
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new List<string> { "song1" });
                return Task.FromException<List<string>>(new Exception(longErrorMessage));
            });
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            null, // no filter
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

    [Fact]
    public async Task CreateWishlistItem_WithFilter_StoresFilterCorrectly()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config
        {
            MusicRepositoryPath = "/data",
            WishlistMaxResultsToHash = 50
        });
        
        var filter = "genre:Pop year:>2020";
        
        // Mock search to return song IDs for the filtered query
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", filter, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<string> { "song1", "song2" }));
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);

        // Act
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            filter,
            CancellationToken.None);

        // Assert
        item.ShouldNotBeNull();
        item.Filter.ShouldBe(filter);
        
        var savedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        savedItem.ShouldNotBeNull();
        savedItem.Filter.ShouldBe(filter);
        
        // Verify the search service was called with the correct filter
        await purchasesSearchService.Received().SearchForHashAsync(
            source.Id, 
            "test query", 
            filter, 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckForUpdates_WithFilter_AppliesFilterFromWishlistItem()
    {
        // Arrange
        var scenario = new Scenario();
        var source = CreateSource(scenario.DbContext);
        
        var sourcesService = Substitute.For<ISourcesService>();
        var purchasesSearchService = Substitute.For<IPurchasesSearchService>();
        
        var filter = "genre:Pop";
        
        // Setup mock to return different values on consecutive calls:
        // First call (creation): initial song IDs
        // Second call (check): different song IDs to trigger update
        var initialSongIds = new List<string> { "initial-song-1", "initial-song-2" };
        var updatedSongIds = new List<string> { "new-song-1", "new-song-2" };
        var callCount = 0;
        purchasesSearchService.SearchForHashAsync(source.Id, "test query", filter, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return Task.FromResult(callCount == 1 ? initialSongIds : updatedSongIds);
            });
        
        var logger = Substitute.For<ILogger<WishlistService>>();
        var config = Options.Create(new Config { MusicRepositoryPath = "/data", WishlistMaxResultsToHash = 50 });
        
        var wishlistService = new WishlistService(scenario.DbContext, sourcesService, purchasesSearchService, config, logger);
        
        // Create item with filter
        var item = await wishlistService.CreateAsync(
            scenario.AdminUser.Id,
            source.Id,
            "test query",
            filter,
            CancellationToken.None);

        // Verify filter was stored
        item.Filter.ShouldBe(filter);

        // Act
        await wishlistService.CheckForUpdatesAsync(CancellationToken.None);

        // Assert - should be marked as updated because the filtered results changed
        var updatedItem = await scenario.DbContext.WishlistItems.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.Status.ShouldBe(WishlistItemStatus.Updated);
        
        // Verify the search was called with the correct filter
        await purchasesSearchService.Received().SearchForHashAsync(
            source.Id, 
            "test query", 
            filter, 
            Arg.Any<CancellationToken>());
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