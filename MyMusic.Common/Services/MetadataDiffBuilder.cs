using MyMusic.Common.Entities;
using MyMusic.Common.Sources;

namespace MyMusic.Common.Services;

/// <summary>
/// Service for building normalized MetadataDiffModel from Song and SourceSong.
/// Shared between manual metadata fetch and auto-fetch background tasks.
/// </summary>
public class MetadataDiffBuilder(
    IApiPathResolver pathResolver,
    IImageComparisonService? imageComparisonService = null)
{
    private readonly string _apiBasePath = pathResolver.ApiBasePath;
    private readonly IImageComparisonService? _imageComparisonService = imageComparisonService;

    /// <summary>
    /// Creates a metadata diff comparing the current song with source metadata.
    /// Constructs the diff at runtime with proper URL formatting.
    /// </summary>
    public async Task<MetadataDiffModel> CreateDiffAsync(
        Song song,
        SourceSong sourceSong,
        CancellationToken cancellationToken = default)
    {
        var diff = new MetadataDiffModel();

        // Title
        if (!string.IsNullOrEmpty(sourceSong.Title) &&
            !string.Equals(song.Title, sourceSong.Title, StringComparison.OrdinalIgnoreCase))
        {
            diff.Title = new MetadataField<string>
            {
                Old = song.Title,
                New = sourceSong.Title,
            };
        }

        // Year
        if (sourceSong.Year.HasValue && sourceSong.Year.Value > 0 && song.Year != sourceSong.Year)
        {
            diff.Year = new MetadataField<int>
            {
                Old = song.Year ?? 0,
                New = sourceSong.Year.Value,
            };
        }

        // Lyrics
        if (!string.IsNullOrWhiteSpace(sourceSong.Lyrics) &&
            !string.Equals(song.Lyrics, sourceSong.Lyrics, StringComparison.OrdinalIgnoreCase))
        {
            diff.Lyrics = new MetadataField<string>
            {
                Old = song.Lyrics ?? string.Empty,
                New = sourceSong.Lyrics,
            };
        }

        // Rating - only include if the source has a rating
        // Note: SourceSong doesn't have Rating property, so we skip this for now
        // If it gets added later, this will need to be updated

        // Explicit - only create diff when values are actually different AND at least one is true
        // This prevents showing a diff when both values are false (the default)
        if (sourceSong.Explicit != song.Explicit && (sourceSong.Explicit || song.Explicit))
        {
            diff.Explicit = new MetadataField<bool>
            {
                Old = song.Explicit,
                New = sourceSong.Explicit,
            };
        }

        // Album
        if (!string.IsNullOrEmpty(sourceSong.Album?.Name) &&
            !string.Equals(song.Album?.Name, sourceSong.Album.Name, StringComparison.OrdinalIgnoreCase))
        {
            diff.Album = new MetadataField<MetadataAlbumModel>
            {
                Old = new MetadataAlbumModel { Name = song.Album?.Name ?? string.Empty },
                New = new MetadataAlbumModel { Name = sourceSong.Album.Name },
            };
        }

        // Album Artist
        if (!string.IsNullOrEmpty(sourceSong.Album?.Artist?.Name) &&
            !string.Equals(song.Album?.Artist?.Name, sourceSong.Album.Artist?.Name, StringComparison.OrdinalIgnoreCase))
        {
            diff.AlbumArtist = new MetadataField<string>
            {
                Old = song.Album?.Artist?.Name ?? string.Empty,
                New = sourceSong.Album.Artist?.Name ?? string.Empty,
            };
        }

        // Artists
        var songArtistNames = song.Artists.Select(a => a.Artist.Name).ToList();
        var sourceArtistNames = sourceSong.Artists.Select(a => a.Name).ToList();

        if (sourceArtistNames.Count > 0 &&
            !songArtistNames.SequenceEqual(sourceArtistNames, StringComparer.OrdinalIgnoreCase))
        {
            diff.Artists = new MetadataField<List<MetadataArtistModel>>
            {
                Old = songArtistNames.Select(n => new MetadataArtistModel { Name = n }).ToList(),
                New = sourceArtistNames.Select(n => new MetadataArtistModel { Name = n }).ToList(),
            };
        }

        // Genres
        var songGenreNames = song.Genres.Select(g => g.Genre.Name).ToList();
        var sourceGenreNames = sourceSong.Genres.ToList();

        if (sourceGenreNames.Count > 0 &&
            !songGenreNames.SequenceEqual(sourceGenreNames, StringComparer.OrdinalIgnoreCase))
        {
            diff.Genres = new MetadataField<List<string>>
            {
                Old = songGenreNames,
                New = sourceGenreNames,
            };
        }

        // Cover - use Biggest variant and proper URL
        if (sourceSong.Cover != null && !string.IsNullOrEmpty(sourceSong.Cover.Biggest))
        {
            var imagesAreDifferent = true;

            if (_imageComparisonService != null && song.Cover != null && song.Cover.Data.Length > 0)
            {
                imagesAreDifferent = await _imageComparisonService.AreImagesDifferentAsync(
                    song.Cover.Data,
                    sourceSong.Cover.Biggest,
                    cancellationToken);
            }

            if (imagesAreDifferent)
            {
                var oldCoverUrl = song.CoverId.HasValue
                    ? $"{_apiBasePath}/artwork/{song.CoverId}"
                    : string.Empty;

                diff.Cover = new MetadataField<string>
                {
                    Old = oldCoverUrl,
                    New = sourceSong.Cover.Biggest,
                };
            }
        }

        return diff;
    }
}
