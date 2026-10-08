using System.Text.RegularExpressions;
using MyMusic.Common.Metadata;
using MyMusic.Common.Utilities;
using Scriban;

namespace MyMusic.Common.NamingStrategies;

public partial class TemplateNamingStrategy(string template) : INamingStrategy
{
    [GeneratedRegex(@"\r?\n[ \t]*")]
    private static partial Regex LineBreaks();


    private readonly Template _compiledTemplate = Template.Parse(template);

    public string Generate(SongMetadata song, NamingMetadata? naming = null)
    {
        var model = new
        {
            song,
            id = song.Id,
            title = song.Title,
            album = song.Album,
            artists = song.Artists,
            genres = song.Genres,
            track = song.Track,
            year = song.Year,
            duration = song.Duration,
            @explicit = song.Explicit,
            simple_label = song.SimpleLabel,
            full_label = song.FullLabel,
            artists_label = song.ArtistsLabel,
            extension = naming?.Extension ?? "",
            original_folder = naming?.OriginalFolder,
            original_name = naming?.OriginalName,
        };

        // A template can be formatted over several lines: line breaks, and the indentation after them, are
        // not part of the path
        var result = LineBreaks().Replace(_compiledTemplate.Render(model), "");

        var segments = result.Split('/');
        var sanitizedSegments = segments.Select(FilenameUtils.SanitizeFilename);
        return Path.Combine(sanitizedSegments.ToArray());
    }
}