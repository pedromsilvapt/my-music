using System.IO.Abstractions;
using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Targets;

namespace MyMusic.Common.Services;

public interface ISongUpdateService
{
    Task<SongUpdateResult> UpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default);

    Task<BatchUpdateResult> BatchUpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default);
}

public class SongUpdateService(
    IFileSystem fileSystem,
    IOptions<Config> config,
    ILogger<SongUpdateService> logger) : ISongUpdateService
{
    private readonly ILogger _logger = logger;

    public async Task<SongUpdateResult> UpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default)
    {
        var song = await LoadSongAsync(db, songId, cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {songId}");
        }

        await ApplyUpdatesAsync(db, song, update, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await UpdateFileAndChecksumAsync(song, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return MapToResult(song);
    }

    public async Task<BatchUpdateResult> BatchUpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default)
    {
        var song = await LoadSongAsync(db, songId, cancellationToken);

        if (song == null)
        {
            return new BatchUpdateResult
            {
                Id = songId,
                Success = false,
                Error = $"Song not found with id {songId}",
            };
        }

        try
        {
            await ApplyUpdatesAsync(db, song, update, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            await UpdateFileAndChecksumAsync(song, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            return new BatchUpdateResult
            {
                Id = songId,
                Success = true,
                Song = MapToResult(song),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update song {SongId}", songId);
            return new BatchUpdateResult
            {
                Id = songId,
                Success = false,
                Error = ex.Message,
            };
        }
    }

    private async Task<Song?> LoadSongAsync(MusicDbContext db, long songId, CancellationToken cancellationToken)
    {
        return await db.Songs
            .Where(s => s.Id == songId)
            .Include(s => s.Owner)
            .Include(s => s.Album)
            .ThenInclude(a => a!.Artist)
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres)
            .ThenInclude(sg => sg.Genre)
            .Include(s => s.Cover)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task ApplyUpdatesAsync(MusicDbContext db, Song song, SongUpdateModel update,
        CancellationToken cancellationToken)
    {
        if (update.Title != null)
        {
            song.Title = update.Title;
        }

        if (update.Year.HasValue)
        {
            song.Year = update.Year;
        }

        if (update.Lyrics != null)
        {
            song.Lyrics = update.Lyrics;
        }

        if (update.Rating.HasValue)
        {
            song.Rating = update.Rating;
        }

        if (update.Explicit.HasValue)
        {
            song.Explicit = update.Explicit.Value;
        }

        if (update.Cover != null)
        {
            await UpdateCoverAsync(db, song, update.Cover, cancellationToken);
        }

        if (update.AlbumId.HasValue || update.AlbumName != null)
        {
            await UpdateAlbumAsync(db, song, update.AlbumId, update.AlbumName, update.AlbumArtistId,
                update.AlbumArtistName, cancellationToken);
        }

        if (update.ArtistIds != null || update.ArtistNames != null)
        {
            await UpdateArtistsAsync(db, song, update.ArtistIds, update.ArtistNames, cancellationToken);
        }

        if (update.GenreIds != null || update.GenreNames != null)
        {
            await UpdateGenresAsync(db, song, update.GenreIds, update.GenreNames, cancellationToken);
        }

        song.ModifiedAt = DateTime.UtcNow;
        song.Label = BuildLabel(song);
    }

    private async Task UpdateCoverAsync(MusicDbContext db, Song song, string coverDataUrl,
        CancellationToken cancellationToken)
    {
        var imageBuffer = await ImageBuffer.FromStringAsync(coverDataUrl, cancellationToken);
        var size = imageBuffer.Size;

        if (song.Cover == null)
        {
            song.Cover = new Artwork
            {
                Data = imageBuffer.Data,
                MimeType = imageBuffer.MimeType,
                Width = size.Width,
                Height = size.Height,
            };
            await db.AddAsync(song.Cover, cancellationToken);
        }
        else
        {
            song.Cover.Data = imageBuffer.Data;
            song.Cover.MimeType = imageBuffer.MimeType;
            song.Cover.Width = size.Width;
            song.Cover.Height = size.Height;
        }
    }

    private async Task UpdateAlbumAsync(MusicDbContext db, Song song, long? albumId, string? albumName,
        long? albumArtistId, string? albumArtistName, CancellationToken cancellationToken)
    {
        Album? album = null;

        if (albumId.HasValue)
        {
            album = await db.Albums.FindAsync([albumId.Value], cancellationToken);
        }
        else if (albumName != null)
        {
            album = await db.Albums
                .FirstOrDefaultAsync(a => a.Name == albumName && a.OwnerId == song.OwnerId, cancellationToken);

            if (album == null)
            {
                Artist? albumArtist = null;

                if (albumArtistId.HasValue)
                {
                    albumArtist = await db.Artists.FindAsync([albumArtistId.Value], cancellationToken);
                }
                else if (albumArtistName != null)
                {
                    albumArtist = await GetOrCreateArtistAsync(db, albumArtistName, song.OwnerId, cancellationToken);
                }
                else if (song.Album.Artist != null)
                {
                    albumArtist = song.Album.Artist;
                }
                else
                {
                    var firstArtist = song.Artists.FirstOrDefault()?.Artist;
                    if (firstArtist != null)
                    {
                        albumArtist = firstArtist;
                    }
                    else
                    {
                        albumArtist =
                            await GetOrCreateArtistAsync(db, "Unknown Artist", song.OwnerId, cancellationToken);
                    }
                }

                album = new Album
                {
                    Name = albumName,
                    Artist = albumArtist!,
                    ArtistId = albumArtist!.Id,
                    OwnerId = song.OwnerId,
                    Owner = song.Owner,
                    SongsCount = 0,
                    CreatedAt = DateTime.UtcNow,
                };
                await db.AddAsync(album, cancellationToken);
            }
        }

        if (album != null)
        {
            song.Album = album;
            song.AlbumId = album.Id;
        }
    }

    private async Task UpdateArtistsAsync(MusicDbContext db, Song song, List<long>? artistIds,
        List<string>? artistNames, CancellationToken cancellationToken)
    {
        var artists = new List<Artist>();

        if (artistIds != null)
        {
            foreach (var artistId in artistIds)
            {
                var artist = await db.Artists.FindAsync([artistId], cancellationToken);
                if (artist != null)
                {
                    artists.Add(artist);
                }
            }
        }

        if (artistNames != null)
        {
            foreach (var artistName in artistNames)
            {
                var artist = await GetOrCreateArtistAsync(db, artistName, song.OwnerId, cancellationToken);
                if (!artists.Any(a => a.Id == artist.Id))
                {
                    artists.Add(artist);
                }
            }
        }

        db.SongArtists.RemoveRange(song.Artists);

        song.Artists = artists.Select(a => new SongArtist
        {
            Song = song,
            SongId = song.Id,
            Artist = a,
            ArtistId = a.Id,
        }).ToList();

        foreach (var artist in artists)
        {
            artist.SongsCount = await db.SongArtists.CountAsync(sa => sa.ArtistId == artist.Id, cancellationToken) + 1;
        }
    }

    private async Task UpdateGenresAsync(MusicDbContext db, Song song, List<long>? genreIds, List<string>? genreNames,
        CancellationToken cancellationToken)
    {
        var genres = new List<Genre>();

        if (genreIds != null)
        {
            foreach (var genreId in genreIds)
            {
                var genre = await db.Genres.FindAsync([genreId], cancellationToken);
                if (genre != null)
                {
                    genres.Add(genre);
                }
            }
        }

        if (genreNames != null)
        {
            foreach (var genreName in genreNames)
            {
                var genre = await GetOrCreateGenreAsync(db, genreName, song.OwnerId, cancellationToken);
                if (!genres.Any(g => g.Id == genre.Id))
                {
                    genres.Add(genre);
                }
            }
        }

        db.SongGenres.RemoveRange(song.Genres);

        song.Genres = genres.Select(g => new SongGenre
        {
            Song = song,
            SongId = song.Id,
            Genre = g,
            GenreId = g.Id,
        }).ToList();
    }

    private async Task<Artist> GetOrCreateArtistAsync(MusicDbContext db, string name, long ownerId,
        CancellationToken cancellationToken)
    {
        var artist = await db.Artists
            .FirstOrDefaultAsync(a => a.Name == name && a.OwnerId == ownerId, cancellationToken);

        if (artist != null)
        {
            return artist;
        }

        artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            SongsCount = 0,
            AlbumsCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        await db.AddAsync(artist, cancellationToken);

        return artist;
    }

    private async Task<Genre> GetOrCreateGenreAsync(MusicDbContext db, string name, long ownerId,
        CancellationToken cancellationToken)
    {
        var genre = await db.Genres
            .FirstOrDefaultAsync(g => g.Name == name && g.OwnerId == ownerId, cancellationToken);

        if (genre != null)
        {
            return genre;
        }

        genre = new Genre
        {
            Name = name,
            OwnerId = ownerId,
        };
        await db.AddAsync(genre, cancellationToken);

        return genre;
    }

    private async Task UpdateFileAndChecksumAsync(Song song, CancellationToken cancellationToken)
    {
        var metadata = EntityConverter.ToSong(song);

        var fileTarget = new FileTarget(fileSystem)
        {
            FilePath = song.RepositoryPath,
            Folder = fileSystem.Path.Join(config.Value.MusicRepositoryPath, song.Owner.Username),
        };

        await fileTarget.SaveMetadata(metadata, cancellationToken);

        await fileTarget.Relocate(cancellationToken);

        song.RepositoryPath = fileTarget.FilePath!;

        song.Label = BuildLabel(song);

        var checksumAlgorithm = new XxHash128();
        song.Checksum = MusicService.CalculateChecksum(fileSystem, checksumAlgorithm, song.RepositoryPath);
        song.ChecksumAlgorithm = checksumAlgorithm.GetType().Name;
    }

    private static string BuildLabel(Song song)
    {
        var artists = string.Join(", ", song.Artists.Select(a => a.Artist.Name));
        var explicitSuffix = song.Explicit ? " (Explicit)" : "";
        return $"{song.Title}{explicitSuffix} - {artists}";
    }

    private static SongUpdateResult MapToResult(Song song)
    {
        return new SongUpdateResult
        {
            Id = song.Id,
            Title = song.Title,
            Label = song.Label,
            Cover = song.CoverId,
            Year = song.Year,
            Lyrics = song.Lyrics,
            Rating = song.Rating,
            Explicit = song.Explicit,
            RepositoryPath = song.RepositoryPath,
            Artists = song.Artists.Select(sa => new SongUpdateArtist
            {
                Id = sa.Artist.Id,
                Name = sa.Artist.Name,
            }).ToList(),
            Album = new SongUpdateAlbum
            {
                Id = song.Album.Id,
                Name = song.Album.Name,
                Artist = song.Album.Artist != null
                    ? new SongUpdateAlbumArtist
                    {
                        Id = song.Album.Artist.Id,
                        Name = song.Album.Artist.Name,
                    }
                    : null,
            },
            Genres = song.Genres.Select(sg => new SongUpdateGenre
            {
                Id = sg.Genre.Id,
                Name = sg.Genre.Name,
            }).ToList(),
        };
    }
}