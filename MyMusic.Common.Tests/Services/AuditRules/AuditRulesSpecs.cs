using System.IO.Abstractions.TestingHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using MyMusic.Common.AudioIntegrity;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Common.Services.AuditRules;
using MyMusic.Common.Tests.AudioIntegrity;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services.AuditRules;

public class AuditRulesSpecs
{
    private readonly Scenario _scenario = new();

    private async Task<int> ScanAndSaveAsync(IAuditRule rule, long ownerId)
    {
        var count = 0;
        await foreach (var nc in rule.Scan(_scenario.DbContext, ownerId))
        {
            _scenario.DbContext.AuditNonConformities.Add(nc);
            await _scenario.DbContext.SaveChangesAsync();
            count++;
        }
        return count;
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
        _scenario.DbContext.Add(artwork);
        _scenario.DbContext.SaveChanges();
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
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Song Without Cover", album: album);
        var rule = new MissingCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync();
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
        nonConformity.AuditRuleId.ShouldBe(1);
    }

    [Fact]
    public async Task MissingCoverAuditRule_Scan_SongWithCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500);
        _scenario.CreateSong("Song With Cover", album: album, coverId: cover.Id);
        var rule = new MissingCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task MissingCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Song Without Cover", album: album);
        var rule = new MissingCoverAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        var firstCount = await _scenario.DbContext.AuditNonConformities.CountAsync();
        firstCount.ShouldBe(1);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task MissingCoverAuditRule_Scan_MultipleSongsWithoutCovers_ReturnsCorrectCount()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song 1", album: album);
        _scenario.CreateSong("Song 2", album: album);
        _scenario.CreateSong("Song 3", album: album);
        var rule = new MissingCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(3);
    }

    #endregion

    #region MissingYearAuditRule Tests

    [Fact]
    public async Task MissingYearAuditRule_Scan_SongWithoutYear_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Song Without Year", album: album);
        var rule = new MissingYearAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 2);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingYearAuditRule_Scan_SongWithYear_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song With Year", album: album, year: 2023);
        var rule = new MissingYearAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingYearAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Year", album: album);
        var rule = new MissingYearAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 2)).ShouldBe(1);
    }

    #endregion

    #region MissingGenresAuditRule Tests

    [Fact]
    public async Task MissingGenresAuditRule_Scan_SongWithoutGenres_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Song Without Genres", album: album);
        var rule = new MissingGenresAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 3);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingGenresAuditRule_Scan_SongWithGenres_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var genre = _scenario.CreateGenre("Rock");
        var song = _scenario.CreateSong("Song With Genres", album: album, genres: [genre]);
        var rule = new MissingGenresAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingGenresAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Genres", album: album);
        var rule = new MissingGenresAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 3)).ShouldBe(1);
    }

    #endregion

    #region MissingLyricsAuditRule Tests

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_SongWithoutLyrics_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Song Without Lyrics", album: album);
        var rule = new MissingLyricsAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 4);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_SongWithEmptyLyrics_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Song With Empty Lyrics", album: album, lyrics: "");
        var rule = new MissingLyricsAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_SongWithLyrics_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song With Lyrics", album: album, lyrics: "These are some lyrics");
        var rule = new MissingLyricsAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingLyricsAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Lyrics", album: album);
        var rule = new MissingLyricsAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 4)).ShouldBe(1);
    }

    #endregion

    #region MediumCoverAuditRule Tests

    [Fact]
    public async Task MediumCoverAuditRule_Scan_CoverInMediumRange_ReturnsOne()
    {
        // Arrange - cover between small (500) and medium (1080) threshold
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(800, 800); // Between 500 and 1080
        var song = _scenario.CreateSong("Song With Medium Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 5);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_CoverAboveMediumThreshold_ReturnsZero()
    {
        // Arrange - cover above medium threshold
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(1200, 1200); // Above 1080
        _scenario.CreateSong("Song With Large Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_CoverBelowSmallThreshold_ReturnsZero()
    {
        // Arrange - cover below small threshold (should be caught by SmallCover rule)
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(400, 400); // Below 500
        _scenario.CreateSong("Song With Small Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Cover", album: album);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MediumCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(800, 800);
        _scenario.CreateSong("Song With Medium Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new MediumCoverAuditRule(Options.Create(config));

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 5)).ShouldBe(1);
    }

    #endregion

    #region SmallCoverAuditRule Tests

    [Fact]
    public async Task SmallCoverAuditRule_Scan_CoverBelowSmallThreshold_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(400, 400); // Below 500
        var song = _scenario.CreateSong("Song With Small Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 6);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_CoverExactlyAtThreshold_ReturnsZero()
    {
        // Arrange - cover exactly at small threshold (500)
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500); // Exactly at threshold
        _scenario.CreateSong("Song With Cover At Threshold", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_CoverAboveThreshold_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(600, 600); // Above 500
        _scenario.CreateSong("Song With Large Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Cover", album: album);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SmallCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(400, 400);
        _scenario.CreateSong("Song With Small Cover", album: album, coverId: cover.Id);
        var config = CreateAuditConfig();
        var rule = new SmallCoverAuditRule(Options.Create(config));

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 6)).ShouldBe(1);
    }

    [Fact]
    public void SmallCoverAuditRule_Description_ContainsThresholdValue()
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
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500, mimeType: "image/png");
        var song = _scenario.CreateSong("Song With PNG Cover", album: album, coverId: cover.Id);
        var rule = new NonJpegCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 7);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_JpegCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500, mimeType: "image/jpeg");
        _scenario.CreateSong("Song With JPEG Cover", album: album, coverId: cover.Id);
        var rule = new NonJpegCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Cover", album: album);
        var rule = new NonJpegCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonJpegCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500, mimeType: "image/png");
        _scenario.CreateSong("Song With PNG Cover", album: album, coverId: cover.Id);
        var rule = new NonJpegCoverAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 7)).ShouldBe(1);
    }

    #endregion

    #region NonSquareCoverAuditRule Tests

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_NonSquareCover_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 600); // Not square
        var song = _scenario.CreateSong("Song With Non-Square Cover", album: album, coverId: cover.Id);
        var rule = new NonSquareCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 8);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_SquareCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 500); // Square
        _scenario.CreateSong("Song With Square Cover", album: album, coverId: cover.Id);
        var rule = new NonSquareCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_NoCover_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Song Without Cover", album: album);
        var rule = new NonSquareCoverAuditRule();

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task NonSquareCoverAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var cover = CreateArtwork(500, 600);
        _scenario.CreateSong("Song With Non-Square Cover", album: album, coverId: cover.Id);
        var rule = new NonSquareCoverAuditRule();

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 8)).ShouldBe(1);
    }

    #endregion

    #region MissingFileAuditRule Tests

    [Fact]
    public async Task MissingFileAuditRule_Scan_SongWithMissingFile_ReturnsOne()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Missing File Song", album: album, repositoryPath: "/music/missing.mp3");
        var fileSystem = new MockFileSystem();
        var rule = new MissingFileAuditRule(fileSystem);

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 10);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
    }

    [Fact]
    public async Task MissingFileAuditRule_Scan_SongWithExistingFile_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var existingPath = "/music/existing.mp3";
        _scenario.CreateSong("Existing File Song", album: album, repositoryPath: existingPath);
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [existingPath] = new MockFileData("audio data"),
        });
        var rule = new MissingFileAuditRule(fileSystem);

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task MissingFileAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        _scenario.CreateSong("Missing File Song", album: album, repositoryPath: "/music/missing.mp3");
        var fileSystem = new MockFileSystem();
        var rule = new MissingFileAuditRule(fileSystem);

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        var firstCount = await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 10);
        firstCount.ShouldBe(1);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 10)).ShouldBe(1);
    }

    [Fact]
    public async Task MissingFileAuditRule_Scan_MultipleMissingFiles_ReturnsCorrectCount()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var existingPath = "/music/exists.mp3";
        _scenario.CreateSong("Missing 1", album: album, repositoryPath: "/music/missing1.mp3");
        _scenario.CreateSong("Missing 2", album: album, repositoryPath: "/music/missing2.mp3");
        _scenario.CreateSong("Existing", album: album, repositoryPath: existingPath);
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [existingPath] = new MockFileData("audio data"),
        });
        var rule = new MissingFileAuditRule(fileSystem);

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

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
        await Should.ThrowAsync<NotSupportedException>(() => rule.Patch(_scenario.DbContext, 1));
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
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Problematic Song", album: album);

        var missingCoverRule = new MissingCoverAuditRule();
        var missingYearRule = new MissingYearAuditRule();
        var missingGenresRule = new MissingGenresAuditRule();
        var missingLyricsRule = new MissingLyricsAuditRule();

        // Act
        var coverCount = await ScanAndSaveAsync(missingCoverRule, _scenario.AdminUser.Id);
        var yearCount = await ScanAndSaveAsync(missingYearRule, _scenario.AdminUser.Id);
        var genresCount = await ScanAndSaveAsync(missingGenresRule, _scenario.AdminUser.Id);
        var lyricsCount = await ScanAndSaveAsync(missingLyricsRule, _scenario.AdminUser.Id);

        // Assert
        coverCount.ShouldBe(1);
        yearCount.ShouldBe(1);
        genresCount.ShouldBe(1);
        lyricsCount.ShouldBe(1);

        var allNonConformities = await _scenario.DbContext.AuditNonConformities.ToListAsync();
        allNonConformities.Count.ShouldBe(4);
        allNonConformities.All(nc => nc.SongId == song.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task DifferentOwners_OnlyScansOwnSongs()
    {
        // Arrange - Clean up any data from previous tests to avoid test pollution
        _scenario.DbContext.AuditNonConformities.RemoveRange(_scenario.DbContext.AuditNonConformities);
        await _scenario.DbContext.SaveChangesAsync();
        _scenario.DbContext.Songs.RemoveRange(_scenario.DbContext.Songs);
        await _scenario.DbContext.SaveChangesAsync();
        _scenario.DbContext.Albums.RemoveRange(_scenario.DbContext.Albums);
        await _scenario.DbContext.SaveChangesAsync();
        _scenario.DbContext.Artists.RemoveRange(_scenario.DbContext.Artists);
        await _scenario.DbContext.SaveChangesAsync();

        // Verify cleanup worked
        var songCountBefore = await _scenario.DbContext.Songs.CountAsync();
        songCountBefore.ShouldBe(0);

        var owner2 = _scenario.CreateUser("Second User", "seconduser");

        var artist1 = _scenario.CreateArtist("Artist 1");
        var album1 = _scenario.CreateAlbum("Album 1", artist1);
        _scenario.CreateSong("Owner 1 Song Without Cover", album: album1);

        var artist2 = new Artist
        {
            Name = "Artist 2",
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
            Owner = owner2,
        };
        _scenario.DbContext.Add(artist2);
        _scenario.DbContext.SaveChanges();

        var album2 = new Album
        {
            Name = "Album 2",
            Artist = artist2,
            SongsCount = 0,
            CreatedAt = DateTime.UtcNow,
            Owner = owner2,
        };
        _scenario.DbContext.Add(album2);
        _scenario.DbContext.SaveChanges();

        // Create song for owner2 manually since CreateSong helper uses _scenario.AdminUser
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
        _scenario.DbContext.Add(song2);
        _scenario.DbContext.SaveChanges();

        var rule = new MissingCoverAuditRule();

        // Act
        var owner1Count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        var owner2Count = await ScanAndSaveAsync(rule, owner2.Id);

        // Assert
        owner1Count.ShouldBe(1);
        owner2Count.ShouldBe(1);

        var owner1NonConformities = await _scenario.DbContext.AuditNonConformities
            .Where(nc => nc.OwnerId == _scenario.AdminUser.Id)
            .ToListAsync();
        owner1NonConformities.Count.ShouldBe(1);

        var owner2NonConformities = await _scenario.DbContext.AuditNonConformities
            .Where(nc => nc.OwnerId == owner2.Id)
            .ToListAsync();
        owner2NonConformities.Count.ShouldBe(1);
    }

    #endregion

    #region FileIntegrityAuditRule Tests

    [Fact]
    public async Task FileIntegrityAuditRule_Scan_CorruptedFile_ReturnsOne()
    {
        // Arrange - Xing claims 10 frames but 120 exist (delta = 110 > 100 -> Corrupted)
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Corrupted Song", album: album, repositoryPath: "/music/corrupted.mp3");
        var data = SyntheticMp3Generator.CreateTruncatedFile(
            claimedFrames: 10, actualFrames: 120, bitrateKbps: 128);

        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/music/corrupted.mp3"] = new MockFileData(data),
        });

        var integrityService = new AudioIntegrityService(Options.Create(new AudioIntegrityConfig()),
            [new Mp3IntegrityValidator(
                Options.Create(new AudioIntegrityConfig()),
                Substitute.For<IFFmpegRunner>(),
                fileSystem,
                NullLogger<Mp3IntegrityValidator>.Instance)],
            NullLogger<AudioIntegrityService>.Instance);

        var config = new AudioIntegrityConfig();
        var rule = new FileIntegrityAuditRule(integrityService, Options.Create(config), NullLogger<FileIntegrityAuditRule>.Instance);

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(1);
        var nonConformity = await _scenario.DbContext.AuditNonConformities.FirstOrDefaultAsync(nc => nc.AuditRuleId == 11);
        nonConformity.ShouldNotBeNull();
        nonConformity.SongId.ShouldBe(song.Id);
        nonConformity.Data.ShouldNotBeNull();
    }

    [Fact]
    public async Task FileIntegrityAuditRule_Scan_CleanFile_ReturnsZero()
    {
        // Arrange
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Clean Song", album: album, repositoryPath: "/music/clean.mp3");
        var data = SyntheticMp3Generator.CreateCleanFile(10, bitrateKbps: 128);

        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/music/clean.mp3"] = new MockFileData(data),
        });

        var integrityService = new AudioIntegrityService(Options.Create(new AudioIntegrityConfig()),
            [new Mp3IntegrityValidator(
                Options.Create(new AudioIntegrityConfig()),
                Substitute.For<IFFmpegRunner>(),
                fileSystem,
                NullLogger<Mp3IntegrityValidator>.Instance)],
            NullLogger<AudioIntegrityService>.Instance);

        var config = new AudioIntegrityConfig();
        var rule = new FileIntegrityAuditRule(integrityService, Options.Create(config), NullLogger<FileIntegrityAuditRule>.Instance);

        // Act
        var count = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task FileIntegrityAuditRule_Scan_DoesNotDuplicateExistingNonConformities()
    {
        // Arrange - Xing claims 10 frames but 120 exist (delta = 110 > 100 -> Corrupted)
        var artist = _scenario.CreateArtist("Test Artist");
        var album = _scenario.CreateAlbum("Test Album", artist);
        var song = _scenario.CreateSong("Corrupted Song", album: album, repositoryPath: "/music/corrupted.mp3");
        var data = SyntheticMp3Generator.CreateTruncatedFile(
            claimedFrames: 10, actualFrames: 120, bitrateKbps: 128);

        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/music/corrupted.mp3"] = new MockFileData(data),
        });

        var integrityService = new AudioIntegrityService(Options.Create(new AudioIntegrityConfig()),
            [new Mp3IntegrityValidator(
                Options.Create(new AudioIntegrityConfig()),
                Substitute.For<IFFmpegRunner>(),
                fileSystem,
                NullLogger<Mp3IntegrityValidator>.Instance)],
            NullLogger<AudioIntegrityService>.Instance);

        var config = new AudioIntegrityConfig();
        var rule = new FileIntegrityAuditRule(integrityService, Options.Create(config), NullLogger<FileIntegrityAuditRule>.Instance);

        // First scan
        await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        var firstCount = await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 11);
        firstCount.ShouldBe(1);

        // Second scan - should not create duplicate
        var secondScanCount = await ScanAndSaveAsync(rule, _scenario.AdminUser.Id);
        secondScanCount.ShouldBe(0);
        (await _scenario.DbContext.AuditNonConformities.CountAsync(nc => nc.AuditRuleId == 11)).ShouldBe(1);
    }

    [Fact]
    public async Task FileIntegrityAuditRule_Patch_ThrowsNotSupportedException()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var integrityService = new AudioIntegrityService(Options.Create(new AudioIntegrityConfig()),
            [new Mp3IntegrityValidator(
                Options.Create(new AudioIntegrityConfig()),
                Substitute.For<IFFmpegRunner>(),
                fileSystem,
                NullLogger<Mp3IntegrityValidator>.Instance)],
            NullLogger<AudioIntegrityService>.Instance);

        var config = new AudioIntegrityConfig();
        var rule = new FileIntegrityAuditRule(integrityService, Options.Create(config), NullLogger<FileIntegrityAuditRule>.Instance);

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(() => rule.Patch(_scenario.DbContext, 1));
    }

    [Fact]
    public void FileIntegrityAuditRule_Properties_AreCorrect()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var integrityService = new AudioIntegrityService(Options.Create(new AudioIntegrityConfig()),[], NullLogger<AudioIntegrityService>.Instance);
        var config = new AudioIntegrityConfig();
        var rule = new FileIntegrityAuditRule(integrityService, Options.Create(config), NullLogger<FileIntegrityAuditRule>.Instance);

        // Assert
        rule.Id.ShouldBe(11L);
        rule.Name.ShouldBe("File Integrity");
        rule.Icon.ShouldBe("IconFileAlert");
        rule.Description.ShouldNotBeNullOrEmpty();
        rule.CustomPage.ShouldBeNull();
    }

    #endregion
}