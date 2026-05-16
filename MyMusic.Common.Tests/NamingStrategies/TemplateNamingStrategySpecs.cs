using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using Shouldly;

namespace MyMusic.Common.Tests.NamingStrategies;

public class TemplateNamingStrategySpecs
{
    [Theory]
    [InlineData("{{ title }}{{ extension }}", "Test Song.mp3")]
    [InlineData("{{ title }}.mp3", "Test Song.mp3")]
    [InlineData("{{ title }}{{ extension }}", "Test Song.flac", ".flac")]
    [InlineData("{{ artists[0].name }}/{{ album.name }}/{{ title }}{{ extension }}", "Artist One/Album Name/Test Song.mp3")]
    [InlineData("{{ year }}/{{ title }}{{ extension }}", "2024/Test Song.mp3")]
    [InlineData("{{ year }}/{{ artists[0].name }}/{{ album.name }}/{{ title }}{{ extension }}", "2024/Artist One/Album Name/Test Song.mp3")]
    [InlineData("{{ title }} - {{ artists_label }}{{ extension }}", "Test Song - Artist One, Artist Two.mp3")]
    [InlineData("{{ simple_label }}{{ extension }}", "Test Song - Artist One, Artist Two.mp3")]
    [InlineData("{{ full_label }}{{ extension }}", "Test Song - Artist One, Artist Two - Album Name.mp3")]
    [InlineData("Music/{{ title }}{{ extension }}", "Music/Test Song.mp3")]
    [InlineData("{{ title | string.downcase }}{{ extension }}", "test song.mp3")]
    [InlineData("{{ title | string.upcase }}{{ extension }}", "TEST SONG.mp3")]
    public void Generate_WithTemplate_ShouldRenderExpectedPath(string template, string expectedPath, string extension = ".mp3")
    {
        var song = CreateTestSong();
        var naming = new NamingMetadata { Extension = extension };
        var strategy = new TemplateNamingStrategy(template);

        var result = strategy.Generate(song, naming);

        result.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData("{{ original_folder ?? year }}/{{ title }}{{ extension }}", "2024/Test Song.mp3", null)]
    [InlineData("{{ original_folder ?? year }}/{{ title }}{{ extension }}", "Original/Path/Test Song.mp3", "Original/Path")]
    [InlineData("{{ original_name ?? title }}{{ extension }}", "Test Song.mp3", null)]
    [InlineData("{{ original_name ?? title }}{{ extension }}", "original-file-name.mp3", null, "original-file-name")]
    public void Generate_WithNamingMetadataFallbacks_ShouldRespectNullCoalescing(
        string template,
        string expectedPath,
        string? originalFolder,
        string? originalName = null)
    {
        var song = CreateTestSong();
        var naming = new NamingMetadata
        {
            Extension = ".mp3",
            OriginalFolder = originalFolder,
            OriginalName = originalName,
        };
        var strategy = new TemplateNamingStrategy(template);

        var result = strategy.Generate(song, naming);

        result.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData("{{ if explicit }}[E] {{ end }}{{ title }}{{ extension }}", "[E] Test Song.mp3", true)]
    [InlineData("{{ if explicit }}[E] {{ end }}{{ title }}{{ extension }}", "Test Song.mp3", false)]
    [InlineData("{{ track }}. {{ title }}{{ extension }}", "5. Test Song.mp3", 5)]
    [InlineData("{{ track ?? 1 }}. {{ title }}{{ extension }}", "1. Test Song.mp3", null)]
    [InlineData("{{ track ?? 1 }}. {{ title }}{{ extension }}", "12. Test Song.mp3", 12)]
    public void Generate_WithConditionalOrOptionalProperties_ShouldRenderCorrectly(
        string template,
        string expectedPath,
        object? propertyValue)
    {
        var song = CreateTestSong(propertyValue switch
        {
            bool explicitFlag => new { Explicit = explicitFlag },
            int track => new { Track = track },
            _ => new { Track = (int?)null },
        });
        var naming = new NamingMetadata { Extension = ".mp3" };
        var strategy = new TemplateNamingStrategy(template);

        var result = strategy.Generate(song, naming);

        result.ShouldBe(expectedPath);
    }

    [Theory]
    [InlineData("{{ album.artist.name }}/{{ title }}{{ extension }}", "Album Artist/Test Song.mp3")]
    [InlineData("{{ album.artist.name ?? artists[0].name }}/{{ title }}{{ extension }}", "Album Artist/Test Song.mp3")]
    public void Generate_WithNestedObjectAccess_ShouldRenderExpectedPath(string template, string expectedPath)
    {
        var song = CreateTestSong(withAlbumArtist: true);
        var naming = new NamingMetadata { Extension = ".mp3" };
        var strategy = new TemplateNamingStrategy(template);

        var result = strategy.Generate(song, naming);

        result.ShouldBe(expectedPath);
    }

    [Fact]
    public void Generate_WithNullYear_ShouldRenderEmptyString()
    {
        var song = CreateTestSong(new { Year = (int?)null });
        var naming = new NamingMetadata { Extension = ".mp3" };
        var strategy = new TemplateNamingStrategy("{{ year }}/{{ title }}{{ extension }}");

        var result = strategy.Generate(song, naming);

        result.ShouldBe("Test Song.mp3");
    }

    [Fact]
    public void Generate_WithNullAlbum_ShouldAccessPropertiesSafely()
    {
        var song = new SongMetadata(null, "Test Song")
        {
            Artists = [new ArtistMetadata(null, "Solo Artist")],
        };
        var naming = new NamingMetadata { Extension = ".mp3" };
        var strategy = new TemplateNamingStrategy("{{ artists[0].name }}/{{ title }}{{ extension }}");

        var result = strategy.Generate(song, naming);

        result.ShouldBe("Solo Artist/Test Song.mp3");
    }

    [Fact]
    public void Generate_WithMultipleArtists_ShouldJoinWithComma()
    {
        var song = new SongMetadata(null, "Collab Song")
        {
            Artists =
            [
                new ArtistMetadata(null, "First Artist"),
                new ArtistMetadata(null, "Second Artist"),
                new ArtistMetadata(null, "Third Artist"),
            ],
        };
        var naming = new NamingMetadata { Extension = ".mp3" };
        var strategy = new TemplateNamingStrategy("{{ artists_label }}{{ extension }}");

        var result = strategy.Generate(song, naming);

        result.ShouldBe("First Artist, Second Artist, Third Artist.mp3");
    }

    [Theory]
    [InlineData("{{ genres[0] }}/{{ title }}{{ extension }}", "Rock/Test Song.mp3")]
    public void Generate_WithGenres_ShouldRenderCorrectly(string template, string expectedPath)
    {
        var song = CreateTestSong();
        song.Genres = ["Rock", "Pop"];
        var naming = new NamingMetadata { Extension = ".mp3" };
        var strategy = new TemplateNamingStrategy(template);

        var result = strategy.Generate(song, naming);

        result.ShouldBe(expectedPath);
    }

    private static SongMetadata CreateTestSong(object? overrides = null, bool withAlbumArtist = false)
    {
        var song = new SongMetadata(null, "Test Song")
        {
            Album = withAlbumArtist
                ? new AlbumMetadata(null, "Album Name", null!, new ArtistMetadata(null, "Album Artist"))
                : new AlbumMetadata(null, "Album Name", null!, null),
            Artists =
            [
                new ArtistMetadata(null, "Artist One"),
                new ArtistMetadata(null, "Artist Two"),
            ],
            Genres = ["Rock"],
            Year = 2024,
            Track = 5,
            Explicit = false,
        };

        if (overrides != null)
        {
            var type = overrides.GetType();
            foreach (var prop in type.GetProperties())
            {
                var value = prop.GetValue(overrides);
                var songProp = typeof(SongMetadata).GetProperty(prop.Name);
                songProp?.SetValue(song, value);
            }
        }

        return song;
    }
}