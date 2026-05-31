using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class SoundalikeMergeServiceSpecs
{
    [Fact]
    public async Task MergeMetadata_FillsMissingYearFromSecondary()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", year: null);
        var secondary = scenario.CreateSong( "Secondary", year: 2020);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Year.ShouldBe(2020);
    }
    
    [Fact]
    public async Task MergeMetadata_KeepsExistingYear()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", year: 2019);
        var secondary = scenario.CreateSong( "Secondary", year: 2020);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Year.ShouldBe(2019);
    }
    
    [Fact]
    public async Task MergeMetadata_FillsMissingLyricsFromSecondary()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", lyrics: null);
        var secondary = scenario.CreateSong( "Secondary", lyrics: "Test lyrics");
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Lyrics.ShouldBe("Test lyrics");
    }
    
    [Fact]
    public async Task MergeMetadata_KeepsExistingLyrics()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", lyrics: "Primary lyrics");
        var secondary = scenario.CreateSong( "Secondary", lyrics: "Secondary lyrics");
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Lyrics.ShouldBe("Primary lyrics");
    }
    
    [Fact]
    public async Task MergeMetadata_FillsMissingBitrateFromSecondary()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", bitrate: null);
        var secondary = scenario.CreateSong( "Secondary", bitrate: 320);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Bitrate.ShouldBe(320);
    }
    
    [Fact]
    public async Task MergeMetadata_KeepsExistingBitrate()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", bitrate: 128);
        var secondary = scenario.CreateSong( "Secondary", bitrate: 320);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Bitrate.ShouldBe(128);
    }
    
    [Fact]
    public async Task MergeMetadata_MergesUniqueArtistsFromSecondaries()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var artistA = scenario.CreateArtist("Artist A");
        var artistB = scenario.CreateArtist("Artist B");
        var artistC = scenario.CreateArtist("Artist C");
        
        var primary = scenario.CreateSong( "Primary", artists: new List<Artist> { artistA });
        var secondary1 = scenario.CreateSong( "Secondary 1", artists: new List<Artist> { artistB });
        var secondary2 = scenario.CreateSong( "Secondary 2", artists: new List<Artist> { artistC });
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary1, secondary2]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        scenario.DbContext.Entry(primary).Collection(s => s.Artists).Load();
        
        primary.Artists.Select(a => a.ArtistId).OrderBy(id => id).ShouldBe(new List<long> { artistA.Id, artistB.Id, artistC.Id }.OrderBy(id => id));
    }
    
    [Fact]
    public async Task MergeMetadata_DoesNotDuplicateExistingArtists()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var artistA = scenario.CreateArtist("Artist A");
        var artistB = scenario.CreateArtist("Artist B");
        
        var primary = scenario.CreateSong( "Primary", artists: new List<Artist> { artistA, artistB });
        var secondary = scenario.CreateSong( "Secondary", artists: new List<Artist> { artistA, artistB });
        
        scenario.DbContext.Entry(primary).Collection(s => s.Artists).Load();
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        scenario.DbContext.Entry(primary).Collection(s => s.Artists).Load();
        
        primary.Artists.Count.ShouldBe(2);
    }
    
    [Fact]
    public async Task MergeMetadata_MergesUniqueGenresFromSecondaries()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var genreA = scenario.CreateGenre("Genre A");
        var genreB = scenario.CreateGenre("Genre B");
        var genreC = scenario.CreateGenre("Genre C");
        
        var primary = scenario.CreateSong( "Primary", genres: new List<Genre> { genreA });
        var secondary1 = scenario.CreateSong( "Secondary 1", genres: new List<Genre> { genreB });
        var secondary2 = scenario.CreateSong( "Secondary 2", genres: new List<Genre> { genreC });
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary1, secondary2]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        scenario.DbContext.Entry(primary).Collection(s => s.Genres).Load();
        
        primary.Genres.Select(g => g.GenreId).OrderBy(id => id).ShouldBe(new List<long> { genreA.Id, genreB.Id, genreC.Id }.OrderBy(id => id));
    }
    
    [Fact]
    public async Task MergeMetadata_DoesNotDuplicateExistingGenres()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var genreA = scenario.CreateGenre("Genre A");
        var genreB = scenario.CreateGenre("Genre B");
        
        var primary = scenario.CreateSong( "Primary", genres: new List<Genre> { genreA, genreB });
        var secondary = scenario.CreateSong( "Secondary", genres: new List<Genre> { genreA, genreB });
        
        scenario.DbContext.Entry(primary).Collection(s => s.Genres).Load();
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        scenario.DbContext.Entry(primary).Collection(s => s.Genres).Load();
        
        primary.Genres.Count.ShouldBe(2);
    }
    
    [Fact]
    public async Task MergeMetadata_UsesFirstSecondaryWithMetadata()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", year: null);
        var secondary1 = scenario.CreateSong( "Secondary 1", year: null);
        var secondary2 = scenario.CreateSong( "Secondary 2", year: 2020);
        var secondary3 = scenario.CreateSong( "Secondary 3", year: 2021);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary1, secondary2, secondary3]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Year.ShouldBe(2020);
    }
    
    [Fact]
    public async Task MergeMetadata_UsesFirstSecondaryWithArtwork()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var artwork = new Artwork
        {
            Data = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            MimeType = "image/png",
            Width = 100,
            Height = 100
        };
        scenario.DbContext.Add(artwork);
        scenario.DbContext.SaveChanges();
        
        var primary = scenario.CreateSong( "Primary", coverId: null);
        var secondary1 = scenario.CreateSong( "Secondary 1", coverId: null);
        var secondary2 = scenario.CreateSong( "Secondary 2", coverId: artwork.Id);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, [secondary1, secondary2]);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.CoverId.ShouldBe(artwork.Id);
    }
    
    [Fact]
    public async Task MergeMetadata_ReturnsEarlyWhenNoSecondaries()
    {
        // Arrange
        var scenario = new Scenario();
        var mergeService = new SoundalikeMergeService(Substitute.For<ILogger<SoundalikeMergeService>>());
        
        var primary = scenario.CreateSong( "Primary", year: null);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, []);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Year.ShouldBeNull();
    }
    
}
