using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public interface ISoundalikeMergeService
{
    Task MergeMetadataAsync(MusicDbContext db, Song primary, List<Song> secondaries, CancellationToken cancellationToken = default);
}

public class SoundalikeMergeService(ILogger<SoundalikeMergeService> logger) : ISoundalikeMergeService
{
    public async Task MergeMetadataAsync(MusicDbContext db, Song primary, List<Song> secondaries, CancellationToken cancellationToken = default)
    {
        if (secondaries.Count == 0)
            return;

        logger.LogDebug("Merging metadata from {Count} secondary songs to primary song {PrimaryId}", secondaries.Count, primary.Id);

        if (primary.Year == null)
        {
            primary.Year = secondaries.FirstOrDefault(s => s.Year != null)?.Year;
        }

        if (string.IsNullOrEmpty(primary.Lyrics))
        {
            primary.Lyrics = secondaries.FirstOrDefault(s => !string.IsNullOrEmpty(s.Lyrics))?.Lyrics;
        }

        if (primary.CoverId == null)
        {
            var firstWithCover = secondaries.FirstOrDefault(s => s.CoverId != null);
            if (firstWithCover != null)
            {
                primary.CoverId = firstWithCover.CoverId;
                primary.Cover = firstWithCover.Cover;
            }
        }

        if (primary.Bitrate == null)
        {
            primary.Bitrate = secondaries.FirstOrDefault(s => s.Bitrate != null)?.Bitrate;
        }

        // The placeholder album only stands in for a missing one, so any merged song's real album replaces it
        if (primary.Album.Name == Album.PlaceholderName)
        {
            var firstWithAlbum = secondaries.FirstOrDefault(s => s.Album.Name != Album.PlaceholderName);
            if (firstWithAlbum != null)
            {
                primary.AlbumId = firstWithAlbum.AlbumId;
                primary.Album = firstWithAlbum.Album;
            }
        }

        var existingArtistIds = primary.Artists.Select(a => a.ArtistId).ToHashSet();
        foreach (var secondary in secondaries)
        {
            foreach (var artist in secondary.Artists)
            {
                if (!existingArtistIds.Contains(artist.ArtistId))
                {
                    var newSongArtist = new SongArtist
                    {
                        SongId = primary.Id,
                        ArtistId = artist.ArtistId,
                        Artist = artist.Artist
                    };
                    primary.Artists.Add(newSongArtist);
                    existingArtistIds.Add(artist.ArtistId);
                }
            }
        }

        // The placeholder artist only stands in for a missing one, so it goes away once the song has a real artist
        if (primary.Artists.Any(a => a.Artist.Name != Artist.PlaceholderName))
        {
            foreach (var placeholder in primary.Artists.Where(a => a.Artist.Name == Artist.PlaceholderName).ToList())
            {
                primary.Artists.Remove(placeholder);

                // Those the merge has just added were never saved, and have nothing to delete
                if (placeholder.Id != 0)
                {
                    db.SongArtists.Remove(placeholder);
                }
            }
        }

        var existingGenreIds = primary.Genres.Select(g => g.GenreId).ToHashSet();
        foreach (var secondary in secondaries)
        {
            foreach (var genre in secondary.Genres)
            {
                if (!existingGenreIds.Contains(genre.GenreId))
                {
                    var newSongGenre = new SongGenre
                    {
                        SongId = primary.Id,
                        GenreId = genre.GenreId,
                        Genre = genre.Genre
                    };
                    primary.Genres.Add(newSongGenre);
                    existingGenreIds.Add(genre.GenreId);
                }
            }
        }

        db.Update(primary);
        await db.SaveChangesAsync(cancellationToken);
        
        logger.LogDebug("Metadata merge complete for primary song {PrimaryId}", primary.Id);
    }
}
