using System.IO.Abstractions.TestingHelpers;
using EntityFrameworkCore.Projectables;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.AuditRules;

public class AuditRulesSpecs : IDisposable
{
    private readonly MusicDbContext _db;
    private readonly SqliteConnection _keepAliveConnection;
    private readonly User _owner;

    public AuditRulesSpecs()
    {
        _keepAliveConnection = new SqliteConnection("DataSource=:memory:");
        _keepAliveConnection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(_keepAliveConnection)
            .UseProjectables()
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _db = new MusicDbContext(options);
        _db.Database.EnsureCreated();
        _db.SaveChanges();

        _owner = CreateUser("Test User", "testuser");
    }

    public void Dispose()
    {
        _db.Dispose();
        _keepAliveConnection.Dispose();
    }

    private async Task<int> ScanAndSaveAsync(IAuditRule rule, long ownerId)
    {
        var count = 0;
        await foreach (var nc in rule.Scan(_db, ownerId))
        {
            _db.AuditNonConformities.Add(nc);
            await _db.SaveChangesAsync();
            count++;
        }
        return count;
    }

    private User CreateUser(string name, string username)
    {
        var user = new User
        {
            Name = name,
            Username = username,
        };
        _db.Add(user);
        _db.SaveChanges();
        return user;
    }

    private Artist CreateArtist(string name)
    {
        var artist = new Artist
        {
            Name = name,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
            Owner = _owner,
        };
        _db.Add(artist);
        _db.SaveChanges();
        return artist;
    }

    private Album CreateAlbum(string name, Artist artist)
    {
        var album = new Album
        {
            Name = name,
            Artist = artist,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
            Owner = _owner,
        };
        _db.Add(album);
        _db.SaveChanges();
        return album;
    }

    private Song CreateSong(string title, Album album, Artwork? cover = null, int? year = null, string? lyrics = null, List<SongGenre>? genres = null, string? repositoryPath = null)
    {
        var song = new Song
        {
            Title = title,
            Label = title,
            Album = album,
            Cover = cover,
            Year = year,
            Lyrics = lyrics,
            Genres = genres ?? [],
            Artists = [],
            Devices = [],
            Sources = [],
            RepositoryPath = repositoryPath ?? $"/test/{title}.mp3",
            Checksum = Guid.NewGuid().ToString(),
            ChecksumAlgorithm = "SHA256",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Owner = _owner,
            Duration = TimeSpan.FromMinutes(3),
        };
        _db.Add(song);
        _db.SaveChanges();
        return song;
    }

    private Genre CreateGenre(string name)
    {
        var genre = new Genre
        {
            Name = name,
            Owner = _owner,
        };
        _db.Add(genre);
        _db.SaveChanges();
        return genre;
    }

    private Artwork CreateArtwork(int width, int height, string mimeType = "image/jpeg")
    {
        var artwork = new Artwork
        {
            Data = [],
            Width = width,
            Height = height,
            MimeType = mimeType,
        };
        _db.Add(artwork);
        _db.SaveChanges();
        return artwork;
    }

    private AuditConfig CreateAuditConfig(int mediumThreshold = 1080, int smallThreshold = 500)
    {
        return new AuditConfig
        {
            MediumCoverThreshold = mediumThreshold,
            SmallCoverThreshold = smallThreshold,
        };
    }

    #region MissingCoverAuditRule Tests

    [Fact]
    public async Task MissingCoverAuditRule_Scan_SongWithoutCover_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Song Without Cover", album, cover: null);
        var rule = new MissingCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync();
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
        nonConformity.AuditRuleId.ShouldBe(1);
    }

    [Fact]
    public async Task MissingCoverAuditRule_Scan_SongWithCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500);
        CreateSong("Song With Cover", album, cover: cover);
        var rule = new MissingCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task MissingCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Song Without Cover", album, cover: null);
        var rule = new MissingCoverAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);
        var firstCount = await _db.AuditNonConformities.CountAsync();
        firstCount.ShouldBe(1);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task MissingCoverAuditRule_Scan_MultipleSongsWithoutCovers_ReturnsCorrectCount()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song 1", album, cover: null);
        CreateSong("Song 2", album, cover: null);
        CreateSong("Song 3", album, cover: null);
        var rule = new MissingCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(3);
    }

    #endregion

    #region MissingYearAuditRule Tests

    [Fact]
    public async Task MissingYearAuditRule_Scan_SongWithoutYear_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Song Without Year", album, year: null);
        var rule = new MissingYearAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 2);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingYearAuditRule_Scan_SongWithYear_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song With Year", album, year: 2023);
        var rule = new MissingYearAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingYearAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Year", album, year: null);
        var rule = new MissingYearAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 2)).ShouldBe(1);
    }

    #endregion

    #region MissingGenresAuditRule Tests

    [Fact]
    public async Task MissingGenresAuditRule_Scan_SongWithoutGenres_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Song Without Genres", album, genres: []);
        var rule = new MissingGenresAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 3);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingGenresAuditRule_Scan_SongWithGenres_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var genre = CreateGenre("Rock");
        var song = CreateSong("Song With Genres", album, genres: [new SongGenre { Genre = genre }]);
        var rule = new MissingGenresAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingGenresAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Genres", album, genres: []);
        var rule = new MissingGenresAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 3)).ShouldBe(1);
    }

    #endregion

    #region MissingLyricsAuditRule Tests

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_SongWithoutLyrics_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Song Without Lyrics", album, lyrics: null);
        var rule = new MissingLyricsAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 4);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_SongWithEmptyLyrics_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Song With Empty Lyrics", album, lyrics: "");
        var rule = new MissingLyricsAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_SongWithLyrics_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song With Lyrics", album, lyrics: "These are some lyrics");
        var rule = new MissingLyricsAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Lyrics", album, lyrics: null);
        var rule = new MissingLyricsAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 4)).ShouldBe(1);
    }

    #endregion

    #region MediumCoverAuditRule Tests

    [Fact]
    public async Task MediumCoverAuditRule_Scan_CoverInMediumRange_ReturnsOne()
    {
        // Arrange - cover between small (500) and medium (1080) threshold
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(800, 800); // Between 500 and 1080
        var song = CreateSong("Song With Medium Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 5);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_CoverAboveMediumThreshold_ReturnsZero()
    {
        // Arrange - cover above medium threshold
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(1200, 1200); // Above 1080
        CreateSong("Song With Large Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_CoverBelowSmallThreshold_ReturnsZero()
    {
        // Arrange - cover below small threshold (should be caught by SmallCover rule)
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(400, 400); // Below 500
        CreateSong("Song With Small Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Cover", album, cover: null);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(800, 800);
        CreateSong("Song With Medium Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 5)).ShouldBe(1);
    }

    #endregion

    #region SmallCoverAuditRule Tests

    [Fact]
    public async Task SmallCoverAuditRule_Scan_CoverBelowSmallThreshold_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(400, 400); // Below 500
        var song = CreateSong("Song With Small Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 6);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_CoverExactlyAtThreshold_ReturnsZero()
    {
        // Arrange - cover exactly at small threshold (500)
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500); // Exactly at threshold
        CreateSong("Song With Cover At Threshold", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_CoverAboveThreshold_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(600, 600); // Above 500
        CreateSong("Song With Large Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Cover", album, cover: null);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(400, 400);
        CreateSong("Song With Small Cover", album, cover: cover);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 6)).ShouldBe(1);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Description_ContainsThresholdValue()
    {
        // Arrange
        var config = CreateAuditConfig(smallThreshold: 600);
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act & Assert
        rule.Description.ShouldContain("600");
    }

    #endregion

    #region NonJpegCoverAuditRule Tests

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_NonJpegCover_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500, mimeType: "image/png");
        var song = CreateSong("Song With PNG Cover", album, cover: cover);
        var rule = new NonJpegCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 7);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_JpegCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500, mimeType: "image/jpeg");
        CreateSong("Song With JPEG Cover", album, cover: cover);
        var rule = new NonJpegCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Cover", album, cover: null);
        var rule = new NonJpegCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500, mimeType: "image/png");
        CreateSong("Song With PNG Cover", album, cover: cover);
        var rule = new NonJpegCoverAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 7)).ShouldBe(1);
    }

    #endregion

    #region NonSquareCoverAuditRule Tests

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_NonSquareCover_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 600); // Not square
        var song = CreateSong("Song With Non-Square Cover", album, cover: cover);
        var rule = new NonSquareCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 8);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_SquareCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500); // Square
        CreateSong("Song With Square Cover", album, cover: cover);
        var rule = new NonSquareCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Song Without Cover", album, cover: null);
        var rule = new NonSquareCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 600);
        CreateSong("Song With Non-Square Cover", album, cover: cover);
        var rule = new NonSquareCoverAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 8)).ShouldBe(1);
    }

    #endregion

    #region MissingFileAuditRule Tests

    [Fact]
    public async Task MissingFileAuditRule_Scan_SongWithMissingFile_ReturnsOne()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Missing File Song", album, repositoryPath: "/music/missing.mp3");
        var fileSystem = new MockFileSystem();
        var rule = new MissingFileAuditRule(fileSystem);

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _db.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 10);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingFileAuditRule_Scan_SongWithExistingFile_ReturnsZero()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var existingPath = "/music/existing.mp3";
        CreateSong("Existing File Song", album, repositoryPath: existingPath);
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [existingPath] = new MockFileData("audio data"),
        });
        var rule = new MissingFileAuditRule(fileSystem);

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingFileAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        CreateSong("Missing File Song", album, repositoryPath: "/music/missing.mp3");
        var fileSystem = new MockFileSystem();
        var rule = new MissingFileAuditRule(fileSystem);

        // First scan
        await ScanAndSaveAsync(rule, _owner.Id);
        var firstCount = await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 10);
        firstCount.ShouldBe(1);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _owner.Id);
        secondScanCount.ShouldBe(0);
        (await _db.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 10)).ShouldBe(1);
    }

    [Fact]
    public async Task MissingFileAuditRule_Scan_MultipleMissingFiles_ReturnsCorrectCount()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var existingPath = "/music/exists.mp3";
        CreateSong("Missing 1", album, repositoryPath: "/music/missing1.mp3");
        CreateSong("Missing 2", album, repositoryPath: "/music/missing2.mp3");
        CreateSong("Existing", album, repositoryPath: existingPath);
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [existingPath] = new MockFileData("audio data"),
        });
        var rule = new MissingFileAuditRule(fileSystem);

        // Act
        var count = await ScanAndSaveAsync(rule, _owner.Id);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task MissingFileAuditRule_Patch_ThrowsNotSupportedException()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var rule = new MissingFileAuditRule(fileSystem);

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(() => rule.Patch(_db, 1));
    }

    [Fact]
    public void MissingFileAuditRule_Properties_AreCorrect()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var rule = new MissingFileAuditRule(fileSystem);

        // Assert
        rule.Id.ShouldBe(10L);
        rule.Name.ShouldBe("Missing Files");
        rule.Icon.ShouldBe("IconFileOff");
        rule.Description.ShouldNotBeNullOrEmpty();
        rule.CustomPage.ShouldBeNull();
    }

    #endregion

    #region Rule Properties Tests

    [Theory]
    [InlineData(typeof(MissingCoverAuditRule), 1L, "Missing Cover", "IconPhotoOff")]
    [InlineData(typeof(MissingYearAuditRule), 2L, "Missing Year", "IconCalendarOff")]
    [InlineData(typeof(MissingGenresAuditRule), 3L, "Missing Genres", "IconTagOff")]
    [InlineData(typeof(MissingLyricsAuditRule), 4L, "Missing Lyrics", "IconTextOff")]
    public void AuditRule_Properties_AreCorrect(Type ruleType, long expectedId, string expectedName, string expectedIcon)
    {
        // Arrange
        var rule = (IAuditRule)Activator.CreateInstance(ruleType)!;

        // Assert
        rule.Id.ShouldBe(expectedId);
        rule.Name.ShouldBe(expectedName);
        rule.Icon.ShouldBe(expectedIcon);
        rule.Description.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(typeof(NonJpegCoverAuditRule), 7L, "Non-JPEG Covers", "IconFileType")]
    [InlineData(typeof(NonSquareCoverAuditRule), 8L, "Non-Square Covers", "IconAspectRatio")]
    public void CoverAuditRule_Properties_AreCorrect(Type ruleType, long expectedId, string expectedName, string expectedIcon)
    {
        // Arrange
        var rule = (IAuditRule)Activator.CreateInstance(ruleType)!;

        // Assert
        rule.Id.ShouldBe(expectedId);
        rule.Name.ShouldBe(expectedName);
        rule.Icon.ShouldBe(expectedIcon);
        rule.Description.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void MediumCoverAuditRule_Properties_AreCorrect()
    {
        // Arrange
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Assert
        rule.Id.ShouldBe(5L);
        rule.Name.ShouldBe("Medium Sized Covers");
        rule.Icon.ShouldBe("IconPhotoDown");
        rule.Description.ShouldContain("1080");
    }

    [Fact]
    public void SmallCoverAuditRule_Properties_AreCorrect()
    {
        // Arrange
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Assert
        rule.Id.ShouldBe(6L);
        rule.Name.ShouldBe("Small Sized Covers");
        rule.Icon.ShouldBe("IconPhotoMinus");
        rule.Description.ShouldContain("500");
    }

    #endregion

    #region Multiple Rules Interaction Tests

    [Fact]
    public async Task MultipleRules_SameSong_DifferentIssues_CreatesMultipleNonConformities()
    {
        // Arrange - song with multiple issues
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Problematic Song", album, cover: null, year: null, lyrics: null, genres: []);

        var missingCoverRule = new MissingCoverAuditRule();
        var missingYearRule = new MissingYearAuditRule();
        var missingGenresRule = new MissingGenresAuditRule();
        var missingLyricsRule = new MissingLyricsAuditRule();

        // Act
        var coverCount = await ScanAndSaveAsync(missingCoverRule, _owner.Id);
        var yearCount = await ScanAndSaveAsync(missingYearRule, _owner.Id);
        var genresCount = await ScanAndSaveAsync(missingGenresRule, _owner.Id);
        var lyricsCount = await ScanAndSaveAsync(missingLyricsRule, _owner.Id);

        // Assert
        coverCount.ShouldBe(1);
        yearCount.ShouldBe(1);
        genresCount.ShouldBe(1);
        lyricsCount.ShouldBe(1);

        var allNonConformities = await _db.AuditNonConformities.ToListAsync();
        allNonConformities.Count.ShouldBe(4);
        allNonConformities.All(nc => nc.SongId == song.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DifferentOwners_OnlyScansOwnSongs()
    {
        // Arrange - Clean up any data from previous tests to avoid test pollution
        _db.AuditNonConformities.RemoveRange(_db.AuditNonConformities);
        await _db.SaveChangesAsync();
        _db.Songs.RemoveRange(_db.Songs);
        await _db.SaveChangesAsync();
        _db.Albums.RemoveRange(_db.Albums);
        await _db.SaveChangesAsync();
        _db.Artists.RemoveRange(_db.Artists);
        await _db.SaveChangesAsync();

        // Verify cleanup worked
        var songCountBefore = await _db.Songs.CountAsync();
        songCountBefore.ShouldBe(0);

        var owner2 = CreateUser("Second User", "seconduser");

        var artist1 = CreateArtist("Artist 1");
        var album1 = CreateAlbum("Album 1", artist1);
        CreateSong("Owner 1 Song Without Cover", album1, cover: null);

        var artist2 = new Artist
        {
            Name = "Artist 2",
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
            Owner = owner2,
        };
        _db.Add(artist2);
        _db.SaveChanges();

        var album2 = new Album
        {
            Name = "Album 2",
            Artist = artist2,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
            Owner = owner2,
        };
        _db.Add(album2);
        _db.SaveChanges();

        // Create song for owner2 manually since CreateSong helper uses _owner
        var song2 = new Song
        {
            Title = "Owner 2 Song Without Cover",
            Label = "Owner 2 Song Without Cover",
            Album = album2,
            Cover = null,
            Genres = [],
            Artists = [],
            Devices = [],
            Sources = [],
            RepositoryPath = "/test/Owner 2 Song Without Cover.mp3",
            Checksum = Guid.NewGuid().ToString(),
            ChecksumAlgorithm = "SHA256",
            AddedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Owner = owner2,
            Duration = TimeSpan.FromMinutes(3),
        };
        _db.Add(song2);
        _db.SaveChanges();

        var rule = new MissingCoverAuditRule();

        // Act
        var owner1Count = await ScanAndSaveAsync(rule, _owner.Id);
        var owner2Count = await ScanAndSaveAsync(rule, owner2.Id);

        // Assert
        owner1Count.ShouldBe(1);
        owner2Count.ShouldBe(1);

        var owner1NonConformities = await _db.AuditNonConformities
            .Where(nc => nc.OwnerId == _owner.Id)
            .ToListAsync();
        owner1NonConformities.Count.ShouldBe(1);

        var owner2NonConformities = await _db.AuditNonConformities
            .Where(nc => nc.OwnerId == owner2.Id)
            .ToListAsync();
        owner2NonConformities.Count.ShouldBe(1);
    }

    #endregion
}
