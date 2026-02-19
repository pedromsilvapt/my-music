using MyMusic.Common.Metadata;
using MyMusic.Common.Utilities;
using Scriban;

namespace MyMusic.Common.NamingStrategies;

public class TemplateNamingStrategy(string template) : INamingStrategy
{
    private readonly Template _compiledTemplate = Template.Parse(template);

    public string Generate(SongMetadata song)
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
        };

        var result = _compiledTemplate.Render(model);

        var segments = result.Split('/');
        var sanitizedSegments = segments.Select(FilenameUtils.SanitizeFilename);
        return Path.Combine(sanitizedSegments.ToArray());
    }
}