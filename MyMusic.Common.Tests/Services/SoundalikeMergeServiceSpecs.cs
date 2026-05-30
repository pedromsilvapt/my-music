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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", year: null);
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", year: 2020);
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", year: 2019);
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", year: 2020);
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", lyrics: null);
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", lyrics: "Test lyrics");
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", lyrics: "Primary lyrics");
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", lyrics: "Secondary lyrics");
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", bitrate: null);
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", bitrate: 320);
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", bitrate: 128);
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", bitrate: 320);
        
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
        
        var artistA = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist A");
        var artistB = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist B");
        var artistC = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist C");
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", artists: new List<Artist> { artistA });
        var secondary1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 1", artists: new List<Artist> { artistB });
        var secondary2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 2", artists: new List<Artist> { artistC });
        
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
        
        var artistA = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist A");
        var artistB = CreateArtist(scenario.DbContext, scenario.AdminUser.Id, "Artist B");
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", artists: new List<Artist> { artistA, artistB });
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", artists: new List<Artist> { artistA, artistB });
        
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
        
        var genreA = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Genre A");
        var genreB = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Genre B");
        var genreC = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Genre C");
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", genres: new List<Genre> { genreA });
        var secondary1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 1", genres: new List<Genre> { genreB });
        var secondary2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 2", genres: new List<Genre> { genreC });
        
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
        
        var genreA = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Genre A");
        var genreB = CreateGenre(scenario.DbContext, scenario.AdminUser.Id, "Genre B");
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", genres: new List<Genre> { genreA, genreB });
        var secondary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary", genres: new List<Genre> { genreA, genreB });
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", year: null);
        var secondary1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 1", year: null);
        var secondary2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 2", year: 2020);
        var secondary3 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 3", year: 2021);
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", coverId: null);
        var secondary1 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 1", coverId: null);
        var secondary2 = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Secondary 2", coverId: artwork.Id);
        
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
        
        var primary = CreateSong(scenario.DbContext, scenario.AdminUser.Id, "Primary", year: null);
        
        // Act
        await mergeService.MergeMetadataAsync(scenario.DbContext, primary, []);
        
        // Assert
        scenario.DbContext.Entry(primary).Reload();
        primary.Year.ShouldBeNull();
    }
    
    private Song CreateSong(
        MusicDbContext db, 
        long ownerId, 
        string title, 
        int? year = null, 
        string? lyrics = null, 
        int? bitrate = null,
        long? coverId = null,
        List<Artist>? artists = null,
        List<Genre>? genres = null)
    {
        var artist = CreateArtist(db, ownerId, $"{title} Artist");
        var album = new Album
        {
            Name = $"{title} Album",
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            ArtistId = artist.Id,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(album);
        db.SaveChanges();
        
        var song = new Song
        {
            Title = title,
            Label = title,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            AlbumId = album.Id,
            Album = album,
            Duration = TimeSpan.FromSeconds(180),
            Size = 5000000,
            RepositoryPath = $"/music/{title}.mp3",
            Checksum = "test-checksum",
            ChecksumAlgorithm = "XxHash128",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Year = year,
            Lyrics = lyrics,
            Bitrate = bitrate,
            CoverId = coverId,
            Artists = [],
            Genres = [],
            Devices = [],
            Sources = []
        };
        db.Add(song);
        db.SaveChanges();
        
        if (artists != null && artists.Count > 0)
        {
            foreach (var a in artists)
            {
                var songArtist = new SongArtist
                {
                    SongId = song.Id,
                    ArtistId = a.Id
                };
                db.Add(songArtist);
            }
            db.SaveChanges();
        }
        
        if (genres != null && genres.Count > 0)
        {
            foreach (var g in genres)
            {
                var songGenre = new SongGenre
                {
                    SongId = song.Id,
                    GenreId = g.Id
                };
                db.Add(songGenre);
            }
            db.SaveChanges();
        }
        
        return song;
    }
    
    private Artist CreateArtist(MusicDbContext db, long ownerId, string name)
    {
        var artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId),
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(artist);
        db.SaveChanges();
        return artist;
    }
    
    private Genre CreateGenre(MusicDbContext db, long ownerId, string name)
    {
        var genre = new Genre
        {
            Name = name,
            OwnerId = ownerId,
            Owner = db.Users.First(u => u.Id == ownerId)
        };
        db.Add(genre);
        db.SaveChanges();
        return genre;
    }
}
