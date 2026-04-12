using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;
using MyMusic.Common.Filters;
using Shouldly;

namespace MyMusic.Common.Tests.Filters;

public class FuzzySearchSpecs
{
    #region InMemoryFilterBuilder Tests

    [Fact]
    public void InMemory_Hyphen_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "AC-DC Thunderstruck" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "AC-DC", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("AC-DC Thunderstruck");
    }

    [Fact]
    public void InMemory_LeadingHyphenInQuery_ProducesSameTermsAsNoHyphen()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Holly Humberstone" },
            new() { Text = "Other Artist" }
        };

        // Act
        var r1 = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Holly Humber", i => i.Text).ToList();
        var r2 = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Holly -Humber", i => i.Text).ToList();
        var r3 = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Holly - Humber", i => i.Text).ToList();

        // Assert
        r1.Count.ShouldBe(1);
        r2.Count.ShouldBe(1);
        r3.Count.ShouldBe(1);
        r1[0].Text.ShouldBe("Holly Humberstone");
        r2[0].Text.ShouldBe("Holly Humberstone");
        r3[0].Text.ShouldBe("Holly Humberstone");
    }

    [Fact]
    public void InMemory_QuerySplit_VerifyTerms()
    {
        // Arrange
        var testQuery1 = "Holly Humber";
        var testQuery2 = "Holly -Humber";
        var testQuery3 = "Holly - Humber";

        var delimiters = new[] {
            ' ', '\t', '\n', '\r',
            '-', '–', '—',
            '(', ')', '{', '}', '[', ']',
            '.', ',', '!', '?', ':', ';',
            '\'', '"', '/', '\\', '|'
        };

        // Act
        var terms1 = testQuery1.ToLower().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        var terms2 = testQuery2.ToLower().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        var terms3 = testQuery3.ToLower().Split(delimiters, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        terms1.ShouldBe(new[] { "holly", "humber" });
        terms2.ShouldBe(new[] { "holly", "humber" });
        terms3.ShouldBe(new[] { "holly", "humber" });
    }

    [Fact]
    public void InMemory_Parentheses_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Song (Remix)" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Song (Remix)", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("Song (Remix)");
    }

    [Fact]
    public void InMemory_SquareBrackets_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Artist [feat. Other]" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Artist [feat. Other]", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("Artist [feat. Other]");
    }

    [Fact]
    public void InMemory_CurlyBraces_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Song {Radio Edit}" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Song {Radio Edit}", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("Song {Radio Edit}");
    }

    [Fact]
    public void InMemory_Punctuation_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Hello, World!" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Hello, World!", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("Hello, World!");
    }

    [Fact]
    public void InMemory_MixedSpecialCharsAndWhitespace_SplitsAll()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Foo-Bar Baz (Qux)" },
            new() { Text = "Foo Bar Baz Qux" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Foo-Bar Baz (Qux)", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void InMemory_EmptyResultAfterSplit_ReturnsAll()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Song One" },
            new() { Text = "Song Two" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "---", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void InMemory_DashCharacters_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "en–dash em—dash" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "en–dash em—dash", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("en–dash em—dash");
    }

    [Fact]
    public void InMemory_Quotes_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Don't Stop" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Don't Stop", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("Don't Stop");
    }

    [Fact]
    public void InMemory_Slashes_SplitsIntoSeparateTerms()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "Rock/Pop" },
            new() { Text = "Other Song" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "Rock/Pop", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("Rock/Pop");
    }

    [Fact]
    public void InMemory_AllTermsMustMatch()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "AC-DC" },
            new() { Text = "AC" },
            new() { Text = "DC" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "AC-DC", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Text.ShouldBe("AC-DC");
    }

    [Fact]
    public void InMemory_PartialMatch_ReturnsNothing()
    {
        // Arrange
        var items = new List<TestItem>
        {
            new() { Text = "AC Only" },
            new() { Text = "DC Only" }
        };

        // Act
        var results = InMemoryFilterBuilder.ApplyFuzzySearch(items, "AC-DC", i => i.Text).ToList();

        // Assert
        results.Count.ShouldBe(0);
    }

    #endregion

    #region FuzzySearchHelper Tests

    [Fact]
    public void Database_Hyphen_SplitsIntoSeparateTerms()
    {
        // Arrange
        var (context, _, songs) = SetupSongsWithSpecialChars();

        // Act
        var query = context.Songs.AsQueryable();
        var results = FuzzySearchHelper.ApplyFuzzySearch(query, "AC-DC", s => s.SearchableText).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("AC-DC Thunderstruck");
    }

    [Fact]
    public void Database_Parentheses_SplitsIntoSeparateTerms()
    {
        // Arrange
        var (context, _, songs) = SetupSongsWithSpecialChars();

        // Act
        var query = context.Songs.AsQueryable();
        var results = FuzzySearchHelper.ApplyFuzzySearch(query, "Song (Remix)", s => s.SearchableText).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("Song (Remix)");
    }

    [Fact]
    public void Database_SquareBrackets_SplitsIntoSeparateTerms()
    {
        // Arrange
        var (context, _, songs) = SetupSongsWithSpecialChars();

        // Act
        var query = context.Songs.AsQueryable();
        var results = FuzzySearchHelper.ApplyFuzzySearch(query, "Artist [feat. Other]", s => s.SearchableText).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("Artist [feat. Other]");
    }

    [Fact]
    public void Database_MixedSpecialCharsAndWhitespace_SplitsAll()
    {
        // Arrange
        var (context, _, songs) = SetupSongsWithSpecialChars();

        // Act
        var query = context.Songs.AsQueryable();
        var results = FuzzySearchHelper.ApplyFuzzySearch(query, "Foo-Bar Baz (Qux)", s => s.SearchableText).ToList();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void Database_AllTermsMustMatch()
    {
        // Arrange
        var (context, _, songs) = SetupSongsWithSpecialChars();

        // Act
        var query = context.Songs.AsQueryable();
        var results = FuzzySearchHelper.ApplyFuzzySearch(query, "AC-DC", s => s.SearchableText).ToList();

        // Assert
        results.Count.ShouldBe(1);
        results[0].Title.ShouldBe("AC-DC Thunderstruck");
    }

    [Fact]
    public void Database_PartialMatch_ReturnsNothing()
    {
        // Arrange
        var (context, _, songs) = SetupSongsWithSpecialChars();

        // Act
        var query = context.Songs.AsQueryable();
        var results = FuzzySearchHelper.ApplyFuzzySearch(query, "AC-DC-Thunderstruck-Extra", s => s.SearchableText).ToList();

        // Assert
        results.Count.ShouldBe(0);
    }

    #endregion

    #region Setup Helpers

    private class TestItem
    {
        public string Text { get; set; } = "";
    }

    private static (MusicDbContext Context, User Owner, List<Song> Songs) 
        SetupSongsWithSpecialChars()
    {
        var context = Scenario.CreateDbContext();

        var owner = new User { Name = "Test User", Username = "testuser" };
        context.Users.Add(owner);
        context.SaveChanges();

        var artists = new List<Artist>
        {
            new() { Name = "AC-DC", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow },
            new() { Name = "Test Artist", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow },
            new() { Name = "Artist", Owner = owner, SongsCount = 0, AlbumsCount = 0, CreatedAt = DateTime.UtcNow },
        };
        context.Artists.AddRange(artists);
        context.SaveChanges();

        var albums = new List<Album>
        {
            new() { Name = "Album One", Year = 2020, Artist = artists[0], Owner = owner, SongsCount = 0, CreatedAt = DateTime.UtcNow },
            new() { Name = "Album Two", Year = 2021, Artist = artists[1], Owner = owner, SongsCount = 0, CreatedAt = DateTime.UtcNow },
            new() { Name = "Album Three", Year = 2022, Artist = artists[2], Owner = owner, SongsCount = 0, CreatedAt = DateTime.UtcNow },
        };
        context.Albums.AddRange(albums);
        context.SaveChanges();

        var songs = new List<Song>
        {
            CreateSong(1, "AC-DC Thunderstruck", albums[0], owner, [artists[0]]),
            CreateSong(2, "Song (Remix)", albums[1], owner, [artists[1]]),
            CreateSong(3, "Artist [feat. Other]", albums[2], owner, [artists[2]]),
            CreateSong(4, "Foo-Bar Baz (Qux)", albums[0], owner, [artists[0]]),
            CreateSong(5, "Foo Bar Baz Qux", albums[1], owner, [artists[1]]),
            CreateSong(6, "Other Song", albums[2], owner, [artists[2]]),
        };

        context.Songs.AddRange(songs);
        context.SaveChanges();

        return (context, owner, songs);
    }

    private static Song CreateSong(long id, string title, Album album, User owner, List<Artist> artists)
    {
        return new Song
        {
            Id = id,
            Title = title,
            Label = $"{title} Label",
            Year = 2020,
            IsFavorite = false,
            Explicit = false,
            Rating = null,
            Album = album,
            AlbumId = album.Id,
            Owner = owner,
            OwnerId = owner.Id,
            RepositoryPath = $"/music/{title}.mp3",
            Checksum = $"checksum-{id}",
            ChecksumAlgorithm = "MD5",
            Duration = TimeSpan.FromMinutes(3),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            AddedAt = DateTime.UtcNow,
            Artists = artists.Select(a => new SongArtist { Artist = a, ArtistId = a.Id }).ToList(),
            Genres = [],
            Devices = [],
            Sources = [],
        };
    }

    #endregion
}
