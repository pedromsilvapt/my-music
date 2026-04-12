using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Services;

public class AcousticFingerprintServiceSpecs : IDisposable
{
    private readonly MusicDbContext _db;
    private readonly SqliteConnection _keepAliveConnection;
    private readonly User _owner;
    private readonly IFpcalcService _fpcalc;
    private readonly AcousticFingerprintService _service;

    public AcousticFingerprintServiceSpecs()
    {
        _keepAliveConnection = new SqliteConnection("DataSource=:memory:");
        _keepAliveConnection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(_keepAliveConnection)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _db = new MusicDbContext(options);
        _db.Database.EnsureCreated();
        _db.SaveChanges();

        _owner = CreateUser("Test User", "testuser");

        _fpcalc = Substitute.For<IFpcalcService>();

        var serviceLogger = Substitute.For<ILogger<AcousticFingerprintService>>();
        _service = new AcousticFingerprintService(_db, _fpcalc, serviceLogger);
    }

    public void Dispose()
    {
        _db.Dispose();
        _keepAliveConnection.Dispose();
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

    private Song CreateSong(string title, Album album)
    {
        var song = new Song
        {
            Title = title,
            Label = title,
            Album = album,
            Genres = [],
            Artists = [],
            Devices = [],
            Sources = [],
            RepositoryPath = $"/test/{title}.mp3",
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

    [Fact]
    public void CompareFingerprints_IdenticalFingerprints_Returns1()
    {
        // Arrange
        var fingerprint = new uint[] { 0x12345678, 0xABCDEF00, 0x55555555, 0xFFFFFFFF };

        // Act
        var (score, offsetA, offsetB) = _service.CompareFingerprints(fingerprint, fingerprint, false);

        // Assert
        score.ShouldBe(1.0);
        offsetA.ShouldBe(0);
        offsetB.ShouldBe(0);
    }

    [Fact]
    public void CompareFingerprints_CompletelyDifferent_ReturnsLowScore()
    {
        // Arrange
        var fingerprintA = new uint[] { 0x00000000, 0x00000000, 0x00000000, 0x00000000 };
        var fingerprintB = new uint[] { 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF };

        // Act
        var (score, _, _) = _service.CompareFingerprints(fingerprintA, fingerprintB, false);

        // Assert
        score.ShouldBe(0.0);
    }

    [Fact]
    public void CompareFingerprints_SimilarFingerprints_ReturnsHighScore()
    {
        // Arrange
        var fingerprintA = new uint[] { 0x12345678, 0xABCDEF00, 0x55555555, 0xFFFFFFFF };
        var fingerprintB = new uint[] { 0x12345678, 0xABCDEF00, 0x55555555, 0xFFFFFFFF };

        // Act
        var (score, _, _) = _service.CompareFingerprints(fingerprintA, fingerprintB, false);

        // Assert
        score.ShouldBe(1.0);
    }

    [Fact]
    public void CompareFingerprints_OffsetFingerprints_FindsBestAlignment()
    {
        // Arrange
        var fingerprintA = new uint[] { 0x11111111, 0x22222222, 0x33333333, 0x44444444 };
        var fingerprintB = new uint[] { 0x00000000, 0x11111111, 0x22222222, 0x33333333, 0x44444444 };

        // Act
        var (score, offsetA, offsetB) = _service.CompareFingerprints(fingerprintA, fingerprintB, false);

        // Assert
        offsetA.ShouldBe(0);
        offsetB.ShouldBe(1);
        score.ShouldBeGreaterThanOrEqualTo(0.8);
    }

    [Fact]
    public void CompareFingerprints_EmptyFingerprints_ReturnsZero()
    {
        // Arrange
        var emptyFingerprint = Array.Empty<uint>();
        var fingerprint = new uint[] { 0x12345678 };

        // Act
        var (scoreA, _, _) = _service.CompareFingerprints(emptyFingerprint, fingerprint, false);
        var (scoreB, _, _) = _service.CompareFingerprints(fingerprint, emptyFingerprint, false);
        var (scoreC, _, _) = _service.CompareFingerprints(emptyFingerprint, emptyFingerprint, false);

        // Assert
        scoreA.ShouldBe(0.0);
        scoreB.ShouldBe(0.0);
        scoreC.ShouldBe(0.0);
    }

    [Fact]
    public void CompareFingerprints_MinLength_UsesShorterLength()
    {
        // Arrange
        var fingerprintA = new uint[] { 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF };
        var fingerprintB = new uint[] { 0xFFFFFFFF, 0xFFFFFFFF };

        // Act & Assert
        // With minLength=true, use the shorter length (2 values = 64 bits)
        var (scoreMinLength, _, _) = _service.CompareFingerprints(fingerprintA, fingerprintB, true);
        scoreMinLength.ShouldBe(1.0);

        // With minLength=false, use the longer length (4 values = 128 bits)
        // but only 64 bits match, so score = 64/128 = 0.5
        var (scoreMaxLength, _, _) = _service.CompareFingerprints(fingerprintA, fingerprintB, false);
        scoreMaxLength.ShouldBe(0.5);
    }

    [Fact]
    public async Task GetOrCreateFingerprintAsync_WhenFpcalcNotAvailable_ReturnsNull()
    {
        // Arrange
        _fpcalc.IsAvailable().Returns(false);
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var song = CreateSong("Test Song", album);

        // Act
        var result = await _service.GetOrCreateFingerprintAsync(song);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindDuplicatesAsync_WithSimilarFingerprints_FindsDuplicates()
    {
        // Arrange
        // Real fingerprint data captured from two "Bleeding Love" files
        var fingerprintA = new uint[]
        {
            465959775,432404831,424016223,424007775,419944543,420698231,423844215,423844199,465795959,465660535,
            465693303,465697367,465959767,432404823,424015959,424007767,419911767,424892791,423844199,465828711,
            465697399,465697367,432405343,432405343,424016223,423983326,134560863,1209212031,1209211007,1212356975,
            3431152239,3431021183,2357275263,2357541503,2223323750,2223250030,2223770222,2157709934,2178699119,
            2187189599,2189426007,2189294933,2206063957,2172509525,2153766775,2153766775,2153774967,2153642359,
            2170419559,2204105079,2187263319,2187296085,2187165013,2187099509,2170380149,2153635701,2153766775,
            2153766775,2153642870,2170354022,2203957750,2187311511,2187311511,2189277591,2189277589,2189269397,
            2189269141,2187304093,3256859839,3256859838,3256745134,1109320894,1109320910,1126098383,60550605,
            328858588,328858604,287042540,287046140,52165068,52030796,306012492,306139404,305529109,309723453,
            290730287,282342187,14037787,14038539,14039563,9853451,12048907,280345291,313834443,318019739,308579515,
            304382123,842301611,842387627,842461419,924213611,878060331,888545833,351609385,485765689,485766699,
            485759535,148179519,142940727,142878263,142877199,147596047,147735887,147770703,198105421,181328717,
            181324613,172870469,172808029,437047645,437047679,437023083,978084203,978116971,718070123,734848363,
            701300075,701366139,701366107,726662987,726662491,722304379,722337147,726660603,726660539,701478331,
            701347259,969783227,1003339711,986560927,978303375,440908191,436713631,503822463,503757887,503755807,
            370323510,909279254,909280262,355638278,343050502,343051090,483625459,340908083,356576307,390095907,
            381740259,377742821
        };

        var fingerprintB = new uint[]
        {
            465951583,432396639,424008031,423975007,420960351,425154935,423844215,457399159,465664631,465693303,
            465955415,465959767,432404823,424016215,424007767,419944535,420960375,420960631,457398631,465795943,
            465697399,465959511,465959775,432405343,424016223,423984223,151338079,1209474175,1213667711,1220746111,
            3431152255,3431283327,2357537407,2357541495,2223258214,2223250030,2223770222,2161904230,2178699127,
            2187320663,2189426007,2189262165,2206031189,2172476757,2153766741,2153766783,2153773951,2170420079,
            2170354031,2203974015,2187066719,2187066719,2186900829,2203874653,2170379613,2153766749,2153766783,
            2153773951,2153642879,2170354030,2203957758,2187311519,2187049375,2187180445,2189277597,2189269405,
            2187304093,2187304093,3256859839,3256859838,3256876206,1109320926,1109320910,1130292687,60550605,
            328858620,333048812,287046636,18602460,52165068,35220812,37577036,37699916,41812253,58130749,
            32781103,13906731,14037515,14040587,14047755,11983371,12057099,28687051,313833867,318015643,308577451,
            305430699,842322091,842526891,842424811,894853995,878060329,888545833,351613481,485765689,485768763,
            217324095,148179519,142884407,142878263,142877199,147616527,147735887,147770703,181328205,181328717,
            172919621,172871493,168613213,437047677,437047675,437018987,978084203,978116971,718070123,734852459,
            701300587,701366139,692977499,726662987,726662491,722304379,722337147,726660603,735032763,701478331,
            701347259,1003339707,986561983,978172319,978303375,437238175,436582591,503757887,503755807,370585630,
            370315286,909279254,909284358,355638278,343050502,477268338,483494258,340908082,390130738,390095907,
            381740259,1451419109
        };

        _fpcalc.IsAvailable().Returns(true);
        _fpcalc.FingerprintAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var path = args.Arg<string>();
                if (path.Contains("Song A"))
                    return new FpcalcResult { Fingerprint = fingerprintA, Duration = 262.32 };
                if (path.Contains("Song B"))
                    return new FpcalcResult { Fingerprint = fingerprintB, Duration = 262.51 };
                return null;
            });

        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var songA = CreateSong("Song A", album);
        var songB = CreateSong("Song B", album);

        // Act
        var groups = await _service.FindDuplicatesAsync(_owner.Id, 0.25, 0.95);

        // Assert
        groups.Count.ShouldBe(1);
        groups[0].Count.ShouldBe(2);
        groups[0].Select(s => s.Id).ShouldContain(songA.Id);
        groups[0].Select(s => s.Id).ShouldContain(songB.Id);
    }

    [Fact]
    public async Task ExcludePairAsync_ExcludesPairCorrectly()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var songA = CreateSong("Song A", album);
        var songB = CreateSong("Song B", album);

        // Act
        await _service.ExcludePairAsync(songA.Id, songB.Id, _owner.Id, "Test reason");

        // Assert
        var isExcluded = await _service.IsExcludedPairAsync(songA.Id, songB.Id, _owner.Id);
        isExcluded.ShouldBeTrue();

        var pairs = await _service.GetExcludedPairsAsync(_owner.Id);
        pairs.Count.ShouldBe(1);
        pairs[0].SongAId.ShouldBe(Math.Min(songA.Id, songB.Id));
        pairs[0].SongBId.ShouldBe(Math.Max(songA.Id, songB.Id));
        pairs[0].Reason.ShouldBe("Test reason");
    }

    [Fact]
    public async Task ExcludePairAsync_NormalizesOrder()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var songA = CreateSong("Song A", album);
        var songB = CreateSong("Song B", album);

        // Act
        // Exclude with reversed IDs
        await _service.ExcludePairAsync(songB.Id, songA.Id, _owner.Id);

        // Assert
        var pairs = await _service.GetExcludedPairsAsync(_owner.Id);
        pairs.Count.ShouldBe(1);
        // Should always store with lower ID first
        pairs[0].SongAId.ShouldBe(Math.Min(songA.Id, songB.Id));
        pairs[0].SongBId.ShouldBe(Math.Max(songA.Id, songB.Id));
    }

    [Fact]
    public async Task ExcludePairAsync_DoesNotDuplicate()
    {
        // Arrange
        var artist = CreateArtist("Test Artist");
        var album = CreateAlbum("Test Album", artist);
        var songA = CreateSong("Song A", album);
        var songB = CreateSong("Song B", album);

        // Act
        await _service.ExcludePairAsync(songA.Id, songB.Id, _owner.Id);
        await _service.ExcludePairAsync(songA.Id, songB.Id, _owner.Id);
        await _service.ExcludePairAsync(songB.Id, songA.Id, _owner.Id);

        // Assert
        var pairs = await _service.GetExcludedPairsAsync(_owner.Id);
        pairs.Count.ShouldBe(1);
    }
}
