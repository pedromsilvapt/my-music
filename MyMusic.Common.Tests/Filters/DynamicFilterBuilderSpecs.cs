using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using Shouldly;

namespace MyMusic.Common.Tests.Filters;

public class DynamicFilterBuilderSpecs
{
    #region Test Data Setup

    private static readonly Dictionary<string, string> FieldMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["artist.name"] = "Artists.Artist.Name",
        ["genre.name"] = "Genres.Genre.Name",
        ["device.name"] = "Devices.Device.Name",
        ["playlist.name"] = "PlaylistSongs.Playlist.Name",
    };

    private static (MusicDbContext DbContext, User Owner, List<Song> Songs, List<Artist> Artists, List<Genre> Genres,
        List<Album> Albums) SetupTestData()
    {
        var context = Scenario.CreateDbContext();

        var owner = new User { Name = "Test User", Username = "testuser" };
        context.Users.Add(owner);
        context.SaveChanges();

        var artists = new List<Artist>
        {
            new() { Name = "Pink Floyd", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow },
            new() { Name = "The Beatles", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow },
            new() { Name = "Queen", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow },
            new()
            {
                Name = "David Gilmour", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Unknown Artist", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow,
            },
        };
        context.Artists.AddRange(artists);
        context.SaveChanges();

        var genres = new List<Genre>
        {
            new() { Name = "Rock", Owner = owner },
            new() { Name = "Progressive Rock", Owner = owner },
            new() { Name = "Psychedelic", Owner = owner },
            new() { Name = "Pop", Owner = owner },
            new() { Name = "Unknown", Owner = owner },
        };
        context.Genres.AddRange(genres);
        context.SaveChanges();

        var albums = new List<Album>
        {
            new()
            {
                Name = "The Dark Side of the Moon", Year = 1973, Artist = artists[0], Owner = owner, SongsCount = 0,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Abbey Road", Year = 1969, Artist = artists[1], Owner = owner, SongsCount = 0,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "A Night at the Opera", Year = 1975, Artist = artists[2], Owner = owner, SongsCount = 0,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Wish You Were Here", Year = 1975, Artist = artists[0], Owner = owner, SongsCount = 0,
                CreatedAt = DateTime.UtcNow,
            },
            new()
            {
                Name = "Unknown Album", Year = 2020, Artist = artists[4], Owner = owner, SongsCount = 0,
                CreatedAt = DateTime.UtcNow,
            },
        };
        context.Albums.AddRange(albums);
        context.SaveChanges();

        var songs = new List<Song>
        {
            CreateSong(1, "Echoes", 1973, true, false, 4.5m, albums[0], owner, [artists[0]], [genres[0], genres[1]]),
            CreateSong(2, "Comfortably Numb", 1979, true, false, 5.0m, albums[0], owner, [artists[0], artists[3]],
                [genres[0]]),
            CreateSong(3, "Come Together", 1969, false, false, 4.0m, albums[1], owner, [artists[1]],
                [genres[0], genres[3]]),
            CreateSong(4, "Bohemian Rhapsody", 1975, true, false, 5.0m, albums[2], owner, [artists[2]],
                [genres[0], genres[1]]),
            CreateSong(5, "Shine On You Crazy Diamond", 1971, true, false, 5.0m, albums[3], owner, [artists[0]],
                [genres[0], genres[1], genres[2]]),
            CreateSong(6, "Here Comes the Sun", 1969, false, false, 4.5m, albums[1], owner, [artists[1]],
                [genres[0], genres[3]]),
            CreateSong(7, "Unknown Song", 2020, false, true, null, albums[4], owner, [artists[4]], [genres[4]]),
            CreateSong(8, "Rock Anthem", 2010, false, true, 3.5m, albums[4], owner, [artists[2], artists[4]],
                [genres[0], genres[4]]),
            CreateSong(9, "Instrumental", 2015, false, false, null, albums[4], owner, [], []),
            CreateSong(10, "Classic Hit", 2000, true, false, 4.0m, albums[0], owner, [artists[0]], [genres[0]]),
        };

        context.Songs.AddRange(songs);
        context.SaveChanges();

        return (context, owner, songs, artists, genres, albums);
    }

    private static Song CreateSong(long id, string title, int? year, bool isFavorite, bool explicitContent,
        decimal? rating, Album album, User owner, List<Artist> artists, List<Genre> genres)
    {
        return new Song
        {
            Id = id,
            Title = title,
            Label = $"{title} Label",
            Year = year,
            IsFavorite = isFavorite,
            Explicit = explicitContent,
            Rating = rating,
            Album = album,
            AlbumId = album.Id,
            Owner = owner,
            OwnerId = owner.Id,
            RepositoryPath = $"/music/{title}.mp3",
            Checksum = $"checksum-{id}",
            ChecksumAlgorithm = "MD5",
            Duration = TimeSpan.FromMinutes(3 + id % 5),
            CreatedAt = new DateTime(2020, 1, 1).AddDays(id * 10),
            ModifiedAt = new DateTime(2020, 1, 1).AddDays(id * 10),
            AddedAt = new DateTime(2020, 1, 1).AddDays(id * 10),
            Artists = artists.Select(a => new SongArtist { Artist = a, ArtistId = a.Id }).ToList(),
            Genres = genres.Select(g => new SongGenre { Genre = g, GenreId = g.Id }).ToList(),
            Devices = [],
            Sources = [],
        };
    }

    private static List<Song> ExecuteFilterOnSqlite(MusicDbContext context, string filter)
    {
        var query = context.Songs
            .Include(s => s.Album)
            .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var filterRequest = FilterDslParser.Parse(filter);
            DynamicFilterBuilder.ResolveEntityPaths(filterRequest, FieldMappings);
            var filterExpression = DynamicFilterBuilder.BuildFilter<Song>(filterRequest);
            query = query.Where(filterExpression);
        }

        return query.OrderBy(s => s.Id).ToList();
    }

    private static List<Song> ExecuteFilterOnMemory(List<Song> songs, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return songs.OrderBy(s => s.Id).ToList();
        }

        var filterRequest = FilterDslParser.Parse(filter);
        DynamicFilterBuilder.ResolveEntityPaths(filterRequest, FieldMappings);
        var filterExpression = DynamicFilterBuilder.BuildFilter<Song>(filterRequest);

        return songs.AsQueryable().Where(filterExpression).OrderBy(s => s.Id).ToList();
    }

    private static void AssertResultsMatch(List<Song> sqliteResults, List<Song> memoryResults, long[] expectedIds)
    {
        var sqliteIds = sqliteResults.Select(s => s.Id).ToArray();
        var memoryIds = memoryResults.Select(s => s.Id).ToArray();

        sqliteIds.ShouldBe(expectedIds, "SQLite results don't match expected");
        memoryIds.ShouldBe(expectedIds, "In-Memory results don't match expected");
        sqliteIds.SequenceEqual(memoryIds).ShouldBeTrue("SQLite and In-Memory results should be identical");
    }

    #endregion

    #region Batch 1: Empty/Basic Cases

    [Fact]
    public void Build_EmptyFilter_ReturnsAllSongs()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "");
        var memoryResults = ExecuteFilterOnMemory(songs, "");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Build_WhitespaceFilter_ReturnsAllSongs()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "   ");
        var memoryResults = ExecuteFilterOnMemory(songs, "   ");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    #endregion

    #region Batch 1: Equality Operators

    [Fact]
    public void Build_TitleEquals_MatchesExactly()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title = ""Echoes""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title = ""Echoes""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Build_TitleEquals_CaseSensitive_ReturnsNoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title = ""echoes""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title = ""echoes""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    [Fact]
    public void Build_TitleNotEquals_ExcludesMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title != ""Echoes""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title != ""Echoes""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Build_YearEquals_MatchesExactly()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year = 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "year = 1973");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Build_YearNotEquals_ExcludesMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year != 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "year != 1973");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Build_IsFavoriteEqualsTrue_ReturnsFavoriteSongs()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void Build_ExplicitEqualsTrue_ReturnsExplicitSongs()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8 });
    }

    [Fact]
    public void Build_ExplicitEqualsFalse_ReturnsNonExplicitSongs()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit = false");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit = false");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    #endregion

    #region Batch 2: Comparison Operators

    [Fact]
    public void Year_Gt_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year > 2000");
        var memoryResults = ExecuteFilterOnMemory(songs, "year > 2000");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8, 9 });
    }

    [Fact]
    public void Year_Gte_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year >= 2000");
        var memoryResults = ExecuteFilterOnMemory(songs, "year >= 2000");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_Lt_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year < 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, "year < 1970");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6 });
    }

    [Fact]
    public void Year_Lte_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year <= 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, "year <= 1970");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6 });
    }

    [Fact]
    public void Rating_Gt_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating > 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating > 4.5");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 4, 5 });
    }

    [Fact]
    public void Rating_Gte_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating >= 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating >= 4.5");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void Rating_Lt_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating < 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating < 4.5");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 8, 10 });
    }

    [Fact]
    public void Rating_Lte_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating <= 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating <= 4.5");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 6, 8, 10 });
    }

    [Fact]
    public void Year_GtAndLt_Range()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year > 1970 and year < 1980");
        var memoryResults = ExecuteFilterOnMemory(songs, "year > 1970 and year < 1980");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    #endregion

    #region Batch 3: String Operators

    [Fact]
    public void Title_Contains_Matches()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""the""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""the""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6, 8 });
    }

    [Fact]
    public void Title_Contains_CaseInsensitive()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""ECHO""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""ECHO""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_StartsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title startsWith ""Echo""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title startsWith ""Echo""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_EndsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title endsWith ""Sun""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title endsWith ""Sun""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 6 });
    }

    [Fact]
    public void Title_Tilde_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title ~ ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title ~ ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 8 });
    }

    [Fact]
    public void Label_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"label contains ""Label""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"label contains ""Label""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void AlbumName_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name contains ""Dark""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name contains ""Dark""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumName_StartsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name startsWith ""The""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name startsWith ""The""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumName_EndsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name endsWith ""Opera""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name endsWith ""Opera""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 4 });
    }

    [Fact]
    public void Title_Contains_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""NonExistent""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""NonExistent""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 4: Boolean Operators

    [Fact]
    public void IsFavorite_IsTrue()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "isFavorite isTrue");
        var memoryResults = ExecuteFilterOnMemory(songs, "isFavorite isTrue");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void IsFavorite_IsFalse()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "isFavorite isFalse");
        var memoryResults = ExecuteFilterOnMemory(songs, "isFavorite isFalse");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6, 7, 8, 9 });
    }

    [Fact]
    public void Explicit_IsTrue()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit isTrue");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit isTrue");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8 });
    }

    [Fact]
    public void Explicit_IsFalse()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit isFalse");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit isFalse");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    #endregion

    #region Batch 4: Null Operators

    [Fact]
    public void Rating_IsNull()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating isNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 9 });
    }

    [Fact]
    public void Rating_IsNotNull()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating isNotNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating isNotNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Lyrics_IsNull()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "lyrics isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "lyrics isNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Title_IsNull_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "title isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "title isNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    [Fact]
    public void Title_IsNotNull_AllMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "title isNotNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "title isNotNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_IsNull_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "year isNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 5: Array Membership (In/NotIn)

    [Fact]
    public void Year_In_List()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year in [1973, 1975]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year in [1973, 1975]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 4 });
    }

    [Fact]
    public void Year_NotIn_List()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year notIn [1969, 1971, 1973]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year notIn [1969, 1971, 1973]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 4, 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_In_SingleValue()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year in [1973]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year in [1973]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_In_List()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title in [""Echoes"", ""Unknown Song""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title in [""Echoes"", ""Unknown Song""]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 7 });
    }

    [Fact]
    public void Explicit_In_Boolean()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit in [true]");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit in [true]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8 });
    }

    [Fact]
    public void Year_In_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year in [1900, 1901]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year in [1900, 1901]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 5: Range (Between)

    [Fact]
    public void Year_Between_Inclusive()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year between 1970 and 1980");
        var memoryResults = ExecuteFilterOnMemory(songs, "year between 1970 and 1980");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void Year_Boundary_Values()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year between 1973 and 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "year between 1973 and 1973");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Rating_Between()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "rating between 4.0 and 5.0");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating between 4.0 and 5.0");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void Year_Between_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year between 1900 and 1950");
        var memoryResults = ExecuteFilterOnMemory(songs, "year between 1900 and 1950");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 6: Single Navigation (Album)

    [Fact]
    public void AlbumName_Eq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name = ""The Dark Side of the Moon""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name = ""The Dark Side of the Moon""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumName_Neq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name != ""Unknown Album""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name != ""Unknown Album""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void AlbumYear_Eq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "album.year = 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.year = 1973");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumYear_Gt()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "album.year > 1974");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.year > 1974");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 4, 5, 7, 8, 9 });
    }

    [Fact]
    public void AlbumYear_Between()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "album.year between 1970 and 1980");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.year between 1970 and 1980");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void AlbumName_StartsWith_Abbey()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name startsWith ""Abbey""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name startsWith ""Abbey""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6 });
    }

    [Fact]
    public void AlbumName_IsNull_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "album.name isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.name isNull");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 7: Collection Filtering - Explicit Any

    [Fact]
    public void Artist_Any_Eq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name = ""Pink Floyd""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name = ""Pink Floyd""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Artist_Any_Neq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name != ""Unknown Artist""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name != ""Unknown Artist""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Artist_Any_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name contains ""Floyd""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name contains ""Floyd""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Genre_Any_Eq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[any].name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[any].name = ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Genre_Any_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[any].name contains ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[any].name contains ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Artist_Any_Eq_NoMatch()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name = ""NonExistent""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name = ""NonExistent""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    [Fact]
    public void Artist_Any_StartsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name startsWith ""Pink""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name startsWith ""Pink""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Genre_Any_EndsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[any].name endsWith ""lic""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[any].name endsWith ""lic""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 5 });
    }

    #endregion

    #region Batch 8: Collection Filtering - Explicit All

    [Fact]
    public void Genre_All_Neq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name != ""Unknown""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name != ""Unknown""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Genre_All_Eq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name = ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 9, 10 });
    }

    [Fact]
    public void Genre_All_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name contains ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name contains ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 9, 10 });
    }

    [Fact]
    public void Artist_All_Contains()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name contains ""Pink""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name contains ""Pink""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 5, 9, 10 });
    }

    [Fact]
    public void Artist_All_Neq()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name != ""Unknown Artist""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name != ""Unknown Artist""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Artist_All_StartsWith()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name startsWith ""Pink""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name startsWith ""Pink""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 5, 9, 10 });
    }

    [Fact]
    public void Genre_All_Neq_IncludesEmptySong()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name != ""Unknown""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name != ""Unknown""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Artist_All_Eq_IncludesEmptySong()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name = ""NonExistent""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name = ""NonExistent""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 9 });
    }

    #endregion

    #region Batch 9: Implicit Quantifiers

    [Fact]
    public void Artist_Eq_ImplicitAny()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Pink Floyd""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Pink Floyd""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Artist_Neq_ImplicitAll()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name != ""Unknown Artist""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name != ""Unknown Artist""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Genre_Contains_ImplicitAny()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre.name contains ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre.name contains ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Artist_In_ImplicitAny()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name in [""Pink Floyd"", ""Queen""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name in [""Pink Floyd"", ""Queen""]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 8, 10 });
    }

    [Fact]
    public void Genre_NotIn_ImplicitAll()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre.name notIn [""Unknown"", ""Pop""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre.name notIn [""Unknown"", ""Pop""]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 9, 10 });
    }

    [Fact]
    public void Artist_NotIn_ImplicitAll()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name notIn [""Unknown Artist""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name notIn [""Unknown Artist""]");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    #endregion

    #region Batch 10: Combined Conditions - And

    [Fact]
    public void YearAndIsFavorite_And()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year >= 1973 and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "year >= 1973 and isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 10 });
    }

    [Fact]
    public void TitleContainsAndYearLt()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""e"" and year < 1975");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""e"" and year < 1975");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 5, 6 });
    }

    [Fact]
    public void ThreeWayAnd()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year >= 1970 and year <= 1980 and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "year >= 1970 and year <= 1980 and isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void AlbumAndYear()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name contains ""Dark"" and year > 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name contains ""Dark"" and year > 1970");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void ExplicitAndRating()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit = false and rating >= 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit = false and rating >= 4.5");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void ArtistAndYear()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Pink Floyd"" and year >= 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Pink Floyd"" and year >= 1973");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    #endregion

    #region Batch 10: Combined Conditions - Or

    [Fact]
    public void TitleContains_Or()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""Echo"" or title contains ""Sun""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""Echo"" or title contains ""Sun""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 6 });
    }

    [Fact]
    public void YearOrIsFavorite()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year > 2010 or isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "year > 2010 or isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 7, 9, 10 });
    }

    [Fact]
    public void ThreeWayOr()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "year = 1969 or year = 1973 or year = 1975");
        var memoryResults = ExecuteFilterOnMemory(songs, "year = 1969 or year = 1973 or year = 1975");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 4, 6 });
    }

    [Fact]
    public void AlbumOrArtist()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults =
            ExecuteFilterOnSqlite(context, @"album.name = ""Abbey Road"" or artist.name = ""Pink Floyd""");
        var memoryResults =
            ExecuteFilterOnMemory(songs, @"album.name = ""Abbey Road"" or artist.name = ""Pink Floyd""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 5, 6, 10 });
    }

    [Fact]
    public void GenreOrYear()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre.name = ""Pop"" or year > 2010");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre.name = ""Pop"" or year > 2010");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6, 7, 9 });
    }

    [Fact]
    public void ArtistOrGenre()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Queen"" or genre.name = ""Psychedelic""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Queen"" or genre.name = ""Psychedelic""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 4, 5, 8 });
    }

    #endregion

    #region Batch 11: Grouped Expressions

    [Fact]
    public void GroupedYearRange()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "(year >= 1970 and year <= 1980)");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year >= 1970 and year <= 1980)");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void GroupedOrCondition()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "(year = 1969 or year = 1973)");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year = 1969 or year = 1973)");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 6 });
    }

    [Fact]
    public void GroupedWithOuterCondition()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "(year >= 1970 and year <= 1980) and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year >= 1970 and year <= 1980) and isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void GroupedOrWithAnd()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "(year = 1969 or isFavorite = true) and explicit = false");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year = 1969 or isFavorite = true) and explicit = false");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void MultipleGroups()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "(year > 1970) and (rating >= 4.0 or isFavorite = true)");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year > 1970) and (rating >= 4.0 or isFavorite = true)");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void NestedGroups()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, "((year >= 1970 and year <= 1980) or year > 2010)");
        var memoryResults = ExecuteFilterOnMemory(songs, "((year >= 1970 and year <= 1980) or year > 2010)");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 7, 9 });
    }

    [Fact]
    public void GroupedCollection()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults =
            ExecuteFilterOnSqlite(context, @"(artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");
        var memoryResults = ExecuteFilterOnMemory(songs, @"(artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 8, 10 });
    }

    [Fact]
    public void GroupedWithCollectionAndScalar()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"(artist.name contains ""Pink"") and year > 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, @"(artist.name contains ""Pink"") and year > 1970");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    #endregion

    #region Batch 12: Complex Combinations

    [Fact]
    public void Complex_YearRangeAndArtist()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(year >= 1970 and year <= 1980) and (artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(year >= 1970 and year <= 1980) and (artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void Complex_FavoriteAndGenre()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"isFavorite = true and (genre.name = ""Rock"" or genre.name = ""Progressive Rock"")");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"isFavorite = true and (genre.name = ""Rock"" or genre.name = ""Progressive Rock"")");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void Complex_AlbumOrAlbumAndYear()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(album.name contains ""Dark"" or album.name contains ""Abbey"") and year > 1970");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(album.name contains ""Dark"" or album.name contains ""Abbey"") and year > 1970");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void Complex_ExplicitOrRatingAndGenre()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults =
            ExecuteFilterOnSqlite(context, @"(explicit = true or rating > 4.0) and genre.name != ""Unknown""");
        var memoryResults =
            ExecuteFilterOnMemory(songs, @"(explicit = true or rating > 4.0) and genre.name != ""Unknown""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void Complex_AllArtistAndAnyGenre()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"artist[all].name != ""Unknown Artist"" and genre[any].name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"artist[all].name != ""Unknown Artist"" and genre[any].name = ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void Complex_BetweenAndArtistAndFavorite()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(year between 1970 and 1980) and artist.name contains ""Pink"" and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(year between 1970 and 1980) and artist.name contains ""Pink"" and isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5 });
    }

    [Fact]
    public void Complex_MultipleCollections()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Pink Floyd"" and genre.name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Pink Floyd"" and genre.name = ""Rock""");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Complex_ArtistInAndBetweenAndFavorite()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(artist.name in [""Pink Floyd"", ""Queen""]) and (year between 1970 and 1980) and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(artist.name in [""Pink Floyd"", ""Queen""]) and (year between 1970 and 1980) and isFavorite = true");

        // Assert
        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    #endregion

    #region Batch 13: Enum Property Filtering

    private static DeviceSyncSessionRecord CreateSyncRecord(
        SyncRecordAction action = SyncRecordAction.Created,
        string filePath = "/test/file.mp3",
        SyncRecordSource source = SyncRecordSource.Device,
        long? songId = null)
    {
        return new DeviceSyncSessionRecord
        {
            Id = 1,
            SessionId = 1,
            FilePath = filePath,
            Action = action,
            Source = source,
            SongId = songId,
            ProcessedAt = DateTime.UtcNow
        };
    }

    private static bool ExecuteFilterOnRecord(DeviceSyncSessionRecord record, string filter)
    {
        var filterRequest = FilterDslParser.Parse(filter);
        var filterExpression = DynamicFilterBuilder.BuildFilter<DeviceSyncSessionRecord>(filterRequest);
        var compiled = filterExpression.Compile();
        return compiled(record);
    }

    [Fact]
    public void Enum_Eq_MatchesExactValue()
    {
        // Arrange
        var record = CreateSyncRecord(action: SyncRecordAction.Error);

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action = ""Error""").ShouldBeTrue();
        ExecuteFilterOnRecord(record, @"action = ""Created""").ShouldBeFalse();
    }

    [Fact]
    public void Enum_Eq_CaseInsensitive()
    {
        // Arrange
        var record = CreateSyncRecord(action: SyncRecordAction.Error);

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action = ""error""").ShouldBeTrue();
        ExecuteFilterOnRecord(record, @"action = ""ERROR""").ShouldBeTrue();
    }

    [Fact]
    public void Enum_Neq_ExcludesMatch()
    {
        // Arrange
        var record = CreateSyncRecord(action: SyncRecordAction.Error);

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action != ""Error""").ShouldBeFalse();
        ExecuteFilterOnRecord(record, @"action != ""Created""").ShouldBeTrue();
    }

    [Fact]
    public void Enum_Contains_FallsBackToEquality()
    {
        // Arrange
        var record = CreateSyncRecord(action: SyncRecordAction.Error);

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action contains ""Error""").ShouldBeTrue();
        ExecuteFilterOnRecord(record, @"action contains ""Created""").ShouldBeFalse();
    }

    [Fact]
    public void Enum_StartsWith_FallsBackToEquality()
    {
        // Arrange
        var record = CreateSyncRecord(action: SyncRecordAction.Error);

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action startsWith ""Error""").ShouldBeTrue();
        ExecuteFilterOnRecord(record, @"action startsWith ""Skipped""").ShouldBeFalse();
    }

    [Fact]
    public void Enum_In_MatchesMultipleValues()
    {
        // Arrange
        var record = CreateSyncRecord(action: SyncRecordAction.Error);

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action in [""Error"", ""Created""]").ShouldBeTrue();
        ExecuteFilterOnRecord(record, @"action in [""Created"", ""Skipped""]").ShouldBeFalse();
    }

    [Fact]
    public void Enum_Source_Eq()
    {
        // Arrange
        var deviceRecord = CreateSyncRecord(source: SyncRecordSource.Device);
        var serverRecord = CreateSyncRecord(source: SyncRecordSource.Server);

        // Act & Assert
        ExecuteFilterOnRecord(deviceRecord, @"source = ""Device""").ShouldBeTrue();
        ExecuteFilterOnRecord(deviceRecord, @"source = ""Server""").ShouldBeFalse();
        ExecuteFilterOnRecord(serverRecord, @"source = ""Server""").ShouldBeTrue();
    }

    [Fact]
    public void Enum_CombinedWithOtherField()
    {
        // Arrange
        var record = CreateSyncRecord(
            action: SyncRecordAction.Error,
            filePath: "/music/song.mp3");

        // Act & Assert
        ExecuteFilterOnRecord(record, @"action = ""Error"" and filePath contains ""music""").ShouldBeTrue();
        ExecuteFilterOnRecord(record, @"action = ""Error"" and filePath contains ""other""").ShouldBeFalse();
    }

    #endregion

    #region BuildFilterFromDsl Unified Method

    [Fact]
    public void BuildFilterFromDsl_EmptyString_ReturnsTrueFilter()
    {
        // Arrange
        var (context, owner, _, _, _, _) = SetupTestData();
        var song = CreateSong(999, "Test", 2020, false, false, null, context.Albums.First(), owner, [], []);

        // Act
        var filter = DynamicFilterBuilder.BuildFilterFromDsl<Song>("");

        // Assert
        var compiled = filter.Compile();
        var result = compiled(song);
        result.ShouldBeTrue();
    }

    [Fact]
    public void BuildFilterFromDsl_WhitespaceString_ReturnsTrueFilter()
    {
        // Arrange
        var (context, owner, _, _, _, _) = SetupTestData();
        var song = CreateSong(999, "Test", 2020, false, false, null, context.Albums.First(), owner, [], []);

        // Act
        var filter = DynamicFilterBuilder.BuildFilterFromDsl<Song>("   ");

        // Assert
        var compiled = filter.Compile();
        var result = compiled(song);
        result.ShouldBeTrue();
    }

    [Fact]
    public void BuildFilterFromDsl_NullString_ReturnsTrueFilter()
    {
        // Arrange
        var (context, owner, _, _, _, _) = SetupTestData();
        var song = CreateSong(999, "Test", 2020, false, false, null, context.Albums.First(), owner, [], []);

        // Act
        var filter = DynamicFilterBuilder.BuildFilterFromDsl<Song>(null);

        // Assert
        var compiled = filter.Compile();
        var result = compiled(song);
        result.ShouldBeTrue();
    }

    [Fact]
    public void BuildFilterFromDsl_WithoutMappings_ParsesFilter()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Song>(@"title = ""Echoes""");
        var results = context.Songs
            .Where(filterExpression)
            .ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("Echoes");
    }

    [Fact]
    public void BuildFilterFromDsl_WithMappings_ResolvesEntityPaths()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Song>(
            @"artist.name = ""Pink Floyd""",
            FieldMappings);
        var results = context.Songs
            .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
            .Where(filterExpression)
            .ToList();

        // Assert
        results.Count.ShouldBe(4);
        results.Select(s => s.Id).OrderBy(id => id).ShouldBe([1, 2, 5, 10]);
    }

    [Fact]
    public void BuildFilterFromDsl_WithMappings_ComplexFilter()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();

        // Act
        var filterExpression = DynamicFilterBuilder.BuildFilterFromDsl<Song>(
            @"artist.name = ""Pink Floyd"" and genre.name = ""Rock""",
            FieldMappings);
        var results = context.Songs
            .Include(s => s.Album)
            .Include(s => s.Artists).ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
            .AsSplitQuery()
            .Where(filterExpression)
            .ToList();

        // Assert
        results.Count.ShouldBe(4);
        results.Select(s => s.Id).OrderBy(id => id).ShouldBe([1, 2, 5, 10]);
    }

    [Fact]
    public void BuildFilterFromDsl_WithoutMappings_ComparesWithOldPattern()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();
        var dsl = @"title contains ""the"" and year > 1970";

        // Old pattern
        var oldRequest = FilterDslParser.Parse(dsl);
        var oldFilter = DynamicFilterBuilder.BuildFilter<Song>(oldRequest);

        // New unified method
        var newFilter = DynamicFilterBuilder.BuildFilterFromDsl<Song>(dsl);

        // Act
        var oldResults = context.Songs.Where(oldFilter).OrderBy(s => s.Id).ToList();
        var newResults = context.Songs.Where(newFilter).OrderBy(s => s.Id).ToList();

        // Assert
        oldResults.Count.ShouldBe(newResults.Count);
        oldResults.Select(s => s.Id).ShouldBe(newResults.Select(s => s.Id));
    }

    [Fact]
    public void BuildFilterFromDsl_WithMappings_ComparesWithOldPattern()
    {
        // Arrange
        var (context, _, songs, _, _, _) = SetupTestData();
        var dsl = @"genre[all].name != ""Unknown""";

        // Old pattern
        var oldRequest = FilterDslParser.Parse(dsl);
        DynamicFilterBuilder.ResolveEntityPaths(oldRequest, FieldMappings);
        var oldFilter = DynamicFilterBuilder.BuildFilter<Song>(oldRequest);

        // New unified method
        var newFilter = DynamicFilterBuilder.BuildFilterFromDsl<Song>(dsl, FieldMappings);

        // Act
        var oldResults = context.Songs
            .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
            .Where(oldFilter)
            .OrderBy(s => s.Id)
            .ToList();
        var newResults = context.Songs
            .Include(s => s.Genres).ThenInclude(sg => sg.Genre)
            .Where(newFilter)
            .OrderBy(s => s.Id)
            .ToList();

        // Assert
        oldResults.Count.ShouldBe(newResults.Count);
        oldResults.Select(s => s.Id).ShouldBe(newResults.Select(s => s.Id));
    }

    #endregion
}
