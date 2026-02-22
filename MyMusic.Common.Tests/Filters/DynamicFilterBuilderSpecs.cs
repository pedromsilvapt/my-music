using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        var keepAliveConnection = new SqliteConnection("DataSource=:memory:");
        keepAliveConnection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(keepAliveConnection)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        var context = new MusicDbContext(options);
        context.Database.EnsureCreated();

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
    public void EmptyFilter_ReturnsAllSongs()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "");
        var memoryResults = ExecuteFilterOnMemory(songs, "");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void WhitespaceFilter_ReturnsAllSongs()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "   ");
        var memoryResults = ExecuteFilterOnMemory(songs, "   ");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    #endregion

    #region Batch 1: Equality Operators

    [Fact]
    public void Title_Eq_MatchesExactly()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title = ""Echoes""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title = ""Echoes""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_Eq_CaseSensitive_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title = ""echoes""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title = ""echoes""");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    [Fact]
    public void Title_Neq_ExcludesMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title != ""Echoes""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title != ""Echoes""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_Eq_MatchesExactly()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year = 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "year = 1973");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Year_Neq_ExcludesMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year != 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "year != 1973");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void IsFavorite_Eq_True()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void Explicit_Eq_True()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8 });
    }

    [Fact]
    public void Explicit_Eq_False()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit = false");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit = false");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    #endregion

    #region Batch 2: Comparison Operators

    [Fact]
    public void Year_Gt_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year > 2000");
        var memoryResults = ExecuteFilterOnMemory(songs, "year > 2000");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8, 9 });
    }

    [Fact]
    public void Year_Gte_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year >= 2000");
        var memoryResults = ExecuteFilterOnMemory(songs, "year >= 2000");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_Lt_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year < 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, "year < 1970");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6 });
    }

    [Fact]
    public void Year_Lte_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year <= 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, "year <= 1970");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6 });
    }

    [Fact]
    public void Rating_Gt_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating > 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating > 4.5");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 4, 5 });
    }

    [Fact]
    public void Rating_Gte_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating >= 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating >= 4.5");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void Rating_Lt_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating < 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating < 4.5");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 8, 10 });
    }

    [Fact]
    public void Rating_Lte_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating <= 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating <= 4.5");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 6, 8, 10 });
    }

    [Fact]
    public void Year_GtAndLt_Range()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year > 1970 and year < 1980");
        var memoryResults = ExecuteFilterOnMemory(songs, "year > 1970 and year < 1980");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    #endregion

    #region Batch 3: String Operators

    [Fact]
    public void Title_Contains_Matches()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""the""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""the""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6, 8 });
    }

    [Fact]
    public void Title_Contains_CaseInsensitive()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""ECHO""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""ECHO""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_StartsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title startsWith ""Echo""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title startsWith ""Echo""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_EndsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title endsWith ""Sun""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title endsWith ""Sun""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 6 });
    }

    [Fact]
    public void Title_Tilde_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title ~ ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title ~ ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 8 });
    }

    [Fact]
    public void Label_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"label contains ""Label""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"label contains ""Label""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void AlbumName_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name contains ""Dark""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name contains ""Dark""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumName_StartsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name startsWith ""The""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name startsWith ""The""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumName_EndsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name endsWith ""Opera""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name endsWith ""Opera""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 4 });
    }

    [Fact]
    public void Title_Contains_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""NonExistent""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""NonExistent""");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 4: Boolean Operators

    [Fact]
    public void IsFavorite_IsTrue()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "isFavorite isTrue");
        var memoryResults = ExecuteFilterOnMemory(songs, "isFavorite isTrue");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void IsFavorite_IsFalse()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "isFavorite isFalse");
        var memoryResults = ExecuteFilterOnMemory(songs, "isFavorite isFalse");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6, 7, 8, 9 });
    }

    [Fact]
    public void Explicit_IsTrue()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit isTrue");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit isTrue");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8 });
    }

    [Fact]
    public void Explicit_IsFalse()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit isFalse");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit isFalse");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    #endregion

    #region Batch 4: Null Operators

    [Fact]
    public void Rating_IsNull()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating isNull");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 9 });
    }

    [Fact]
    public void Rating_IsNotNull()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating isNotNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating isNotNull");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Lyrics_IsNull()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "lyrics isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "lyrics isNull");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Title_IsNull_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "title isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "title isNull");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    [Fact]
    public void Title_IsNotNull_AllMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "title isNotNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "title isNotNull");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_IsNull_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "year isNull");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 5: Array Membership (In/NotIn)

    [Fact]
    public void Year_In_List()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year in [1973, 1975]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year in [1973, 1975]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 4 });
    }

    [Fact]
    public void Year_NotIn_List()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year notIn [1969, 1971, 1973]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year notIn [1969, 1971, 1973]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 4, 7, 8, 9, 10 });
    }

    [Fact]
    public void Year_In_SingleValue()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year in [1973]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year in [1973]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Title_In_List()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title in [""Echoes"", ""Unknown Song""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title in [""Echoes"", ""Unknown Song""]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 7 });
    }

    [Fact]
    public void Explicit_In_Boolean()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit in [true]");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit in [true]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 7, 8 });
    }

    [Fact]
    public void Year_In_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year in [1900, 1901]");
        var memoryResults = ExecuteFilterOnMemory(songs, "year in [1900, 1901]");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 5: Range (Between)

    [Fact]
    public void Year_Between_Inclusive()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year between 1970 and 1980");
        var memoryResults = ExecuteFilterOnMemory(songs, "year between 1970 and 1980");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void Year_Boundary_Values()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year between 1973 and 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "year between 1973 and 1973");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1 });
    }

    [Fact]
    public void Rating_Between()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "rating between 4.0 and 5.0");
        var memoryResults = ExecuteFilterOnMemory(songs, "rating between 4.0 and 5.0");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void Year_Between_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year between 1900 and 1950");
        var memoryResults = ExecuteFilterOnMemory(songs, "year between 1900 and 1950");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 6: Single Navigation (Album)

    [Fact]
    public void AlbumName_Eq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name = ""The Dark Side of the Moon""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name = ""The Dark Side of the Moon""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumName_Neq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name != ""Unknown Album""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name != ""Unknown Album""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void AlbumYear_Eq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "album.year = 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.year = 1973");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void AlbumYear_Gt()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "album.year > 1974");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.year > 1974");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 4, 5, 7, 8, 9 });
    }

    [Fact]
    public void AlbumYear_Between()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "album.year between 1970 and 1980");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.year between 1970 and 1980");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void AlbumName_StartsWith_Abbey()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name startsWith ""Abbey""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name startsWith ""Abbey""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6 });
    }

    [Fact]
    public void AlbumName_IsNull_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "album.name isNull");
        var memoryResults = ExecuteFilterOnMemory(songs, "album.name isNull");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    #endregion

    #region Batch 7: Collection Filtering - Explicit Any

    [Fact]
    public void Artist_Any_Eq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name = ""Pink Floyd""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name = ""Pink Floyd""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Artist_Any_Neq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name != ""Unknown Artist""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name != ""Unknown Artist""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Artist_Any_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name contains ""Floyd""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name contains ""Floyd""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Genre_Any_Eq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[any].name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[any].name = ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Genre_Any_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[any].name contains ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[any].name contains ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Artist_Any_Eq_NoMatch()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name = ""NonExistent""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name = ""NonExistent""");

        AssertResultsMatch(sqliteResults, memoryResults, Array.Empty<long>());
    }

    [Fact]
    public void Artist_Any_StartsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[any].name startsWith ""Pink""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[any].name startsWith ""Pink""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Genre_Any_EndsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[any].name endsWith ""lic""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[any].name endsWith ""lic""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 5 });
    }

    #endregion

    #region Batch 8: Collection Filtering - Explicit All

    [Fact]
    public void Genre_All_Neq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name != ""Unknown""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name != ""Unknown""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Genre_All_Eq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name = ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 2, 9, 10 });
    }

    [Fact]
    public void Genre_All_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name contains ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name contains ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 9, 10 });
    }

    [Fact]
    public void Artist_All_Contains()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name contains ""Pink""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name contains ""Pink""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 5, 9, 10 });
    }

    [Fact]
    public void Artist_All_Neq()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name != ""Unknown Artist""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name != ""Unknown Artist""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Artist_All_StartsWith()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name startsWith ""Pink""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name startsWith ""Pink""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 5, 9, 10 });
    }

    [Fact]
    public void Genre_All_Neq_IncludesEmptySong()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre[all].name != ""Unknown""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre[all].name != ""Unknown""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Artist_All_Eq_IncludesEmptySong()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist[all].name = ""NonExistent""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist[all].name = ""NonExistent""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 9 });
    }

    #endregion

    #region Batch 9: Implicit Quantifiers

    [Fact]
    public void Artist_Eq_ImplicitAny()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Pink Floyd""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Pink Floyd""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Artist_Neq_ImplicitAll()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name != ""Unknown Artist""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name != ""Unknown Artist""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    [Fact]
    public void Genre_Contains_ImplicitAny()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre.name contains ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre.name contains ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 8, 10 });
    }

    [Fact]
    public void Artist_In_ImplicitAny()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name in [""Pink Floyd"", ""Queen""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name in [""Pink Floyd"", ""Queen""]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 8, 10 });
    }

    [Fact]
    public void Genre_NotIn_ImplicitAll()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre.name notIn [""Unknown"", ""Pop""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre.name notIn [""Unknown"", ""Pop""]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 9, 10 });
    }

    [Fact]
    public void Artist_NotIn_ImplicitAll()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name notIn [""Unknown Artist""]");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name notIn [""Unknown Artist""]");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 9, 10 });
    }

    #endregion

    #region Batch 10: Combined Conditions - And

    [Fact]
    public void YearAndIsFavorite_And()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year >= 1973 and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "year >= 1973 and isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 10 });
    }

    [Fact]
    public void TitleContainsAndYearLt()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""e"" and year < 1975");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""e"" and year < 1975");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 5, 6 });
    }

    [Fact]
    public void ThreeWayAnd()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year >= 1970 and year <= 1980 and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "year >= 1970 and year <= 1980 and isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void AlbumAndYear()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"album.name contains ""Dark"" and year > 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, @"album.name contains ""Dark"" and year > 1970");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void ExplicitAndRating()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "explicit = false and rating >= 4.5");
        var memoryResults = ExecuteFilterOnMemory(songs, "explicit = false and rating >= 4.5");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void ArtistAndYear()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Pink Floyd"" and year >= 1973");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Pink Floyd"" and year >= 1973");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    #endregion

    #region Batch 10: Combined Conditions - Or

    [Fact]
    public void TitleContains_Or()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"title contains ""Echo"" or title contains ""Sun""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"title contains ""Echo"" or title contains ""Sun""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 6 });
    }

    [Fact]
    public void YearOrIsFavorite()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year > 2010 or isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "year > 2010 or isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 7, 9, 10 });
    }

    [Fact]
    public void ThreeWayOr()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "year = 1969 or year = 1973 or year = 1975");
        var memoryResults = ExecuteFilterOnMemory(songs, "year = 1969 or year = 1973 or year = 1975");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 4, 6 });
    }

    [Fact]
    public void AlbumOrArtist()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults =
            ExecuteFilterOnSqlite(context, @"album.name = ""Abbey Road"" or artist.name = ""Pink Floyd""");
        var memoryResults =
            ExecuteFilterOnMemory(songs, @"album.name = ""Abbey Road"" or artist.name = ""Pink Floyd""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 5, 6, 10 });
    }

    [Fact]
    public void GenreOrYear()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"genre.name = ""Pop"" or year > 2010");
        var memoryResults = ExecuteFilterOnMemory(songs, @"genre.name = ""Pop"" or year > 2010");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 3, 6, 7, 9 });
    }

    [Fact]
    public void ArtistOrGenre()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Queen"" or genre.name = ""Psychedelic""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Queen"" or genre.name = ""Psychedelic""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 4, 5, 8 });
    }

    #endregion

    #region Batch 11: Grouped Expressions

    [Fact]
    public void GroupedYearRange()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "(year >= 1970 and year <= 1980)");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year >= 1970 and year <= 1980)");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void GroupedOrCondition()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "(year = 1969 or year = 1973)");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year = 1969 or year = 1973)");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 3, 6 });
    }

    [Fact]
    public void GroupedWithOuterCondition()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "(year >= 1970 and year <= 1980) and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year >= 1970 and year <= 1980) and isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void GroupedOrWithAnd()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "(year = 1969 or isFavorite = true) and explicit = false");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year = 1969 or isFavorite = true) and explicit = false");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void MultipleGroups()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "(year > 1970) and (rating >= 4.0 or isFavorite = true)");
        var memoryResults = ExecuteFilterOnMemory(songs, "(year > 1970) and (rating >= 4.0 or isFavorite = true)");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void NestedGroups()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, "((year >= 1970 and year <= 1980) or year > 2010)");
        var memoryResults = ExecuteFilterOnMemory(songs, "((year >= 1970 and year <= 1980) or year > 2010)");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 7, 9 });
    }

    [Fact]
    public void GroupedCollection()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults =
            ExecuteFilterOnSqlite(context, @"(artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");
        var memoryResults = ExecuteFilterOnMemory(songs, @"(artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 8, 10 });
    }

    [Fact]
    public void GroupedWithCollectionAndScalar()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"(artist.name contains ""Pink"") and year > 1970");
        var memoryResults = ExecuteFilterOnMemory(songs, @"(artist.name contains ""Pink"") and year > 1970");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    #endregion

    #region Batch 12: Complex Combinations

    [Fact]
    public void Complex_YearRangeAndArtist()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(year >= 1970 and year <= 1980) and (artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(year >= 1970 and year <= 1980) and (artist.name = ""Pink Floyd"" or artist.name = ""Queen"")");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    [Fact]
    public void Complex_FavoriteAndGenre()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"isFavorite = true and (genre.name = ""Rock"" or genre.name = ""Progressive Rock"")");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"isFavorite = true and (genre.name = ""Rock"" or genre.name = ""Progressive Rock"")");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 10 });
    }

    [Fact]
    public void Complex_AlbumOrAlbumAndYear()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(album.name contains ""Dark"" or album.name contains ""Abbey"") and year > 1970");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(album.name contains ""Dark"" or album.name contains ""Abbey"") and year > 1970");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 10 });
    }

    [Fact]
    public void Complex_ExplicitOrRatingAndGenre()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults =
            ExecuteFilterOnSqlite(context, @"(explicit = true or rating > 4.0) and genre.name != ""Unknown""");
        var memoryResults =
            ExecuteFilterOnMemory(songs, @"(explicit = true or rating > 4.0) and genre.name != ""Unknown""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void Complex_AllArtistAndAnyGenre()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"artist[all].name != ""Unknown Artist"" and genre[any].name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"artist[all].name != ""Unknown Artist"" and genre[any].name = ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 3, 4, 5, 6, 10 });
    }

    [Fact]
    public void Complex_BetweenAndArtistAndFavorite()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(year between 1970 and 1980) and artist.name contains ""Pink"" and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(year between 1970 and 1980) and artist.name contains ""Pink"" and isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5 });
    }

    [Fact]
    public void Complex_MultipleCollections()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context, @"artist.name = ""Pink Floyd"" and genre.name = ""Rock""");
        var memoryResults = ExecuteFilterOnMemory(songs, @"artist.name = ""Pink Floyd"" and genre.name = ""Rock""");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Complex_ArtistInAndBetweenAndFavorite()
    {
        var (context, _, songs, _, _, _) = SetupTestData();

        var sqliteResults = ExecuteFilterOnSqlite(context,
            @"(artist.name in [""Pink Floyd"", ""Queen""]) and (year between 1970 and 1980) and isFavorite = true");
        var memoryResults = ExecuteFilterOnMemory(songs,
            @"(artist.name in [""Pink Floyd"", ""Queen""]) and (year between 1970 and 1980) and isFavorite = true");

        AssertResultsMatch(sqliteResults, memoryResults, new long[] { 1, 2, 4, 5 });
    }

    #endregion
}
