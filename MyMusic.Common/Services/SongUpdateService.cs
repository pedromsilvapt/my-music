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
            logger.LogError(ex, "Failed to update song {SongId}", songId);
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
        if (update.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(update.Title.NewValue))
            {
                throw new ValidationException("Title cannot be empty");
            }
            song.Title = update.Title.NewValue;
        }

        if (update.Year is not null)
        {
            song.Year = update.Year.NewValue;
        }

        if (update.Lyrics is not null)
        {
            song.Lyrics = string.IsNullOrWhiteSpace(update.Lyrics.NewValue) ? null : update.Lyrics.NewValue;
        }

        if (update.Rating is not null)
        {
            song.Rating = update.Rating.NewValue;
        }

        if (update.Explicit is not null)
        {
            song.Explicit = update.Explicit.NewValue ?? false;
        }

        if (update.Cover is not null)
        {
            var artworkRef = update.Cover.NewValue;
            if (artworkRef is null)
            {
                await RemoveCoverAsync(db, song);
            }
            else if (artworkRef.Id.HasValue)
            {
                var existingCover = await db.Artworks.FindAsync([artworkRef.Id.Value], cancellationToken);
                if (existingCover is not null)
                {
                    if (song.CoverId != existingCover.Id)
                    {
                        var oldArtwork = song.Cover;
                        song.Cover = existingCover;
                        song.CoverId = existingCover.Id;
                        if (oldArtwork is not null)
                        {
                            await TryDeleteArtworkAsync(db, oldArtwork);
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(artworkRef.Base64))
            {
                await UpdateCoverAsync(db, song, artworkRef.Base64, cancellationToken);
            }
            else
            {
                await RemoveCoverAsync(db, song);
            }
        }

        if (update.Album is not null)
        {
            await UpdateAlbumAsync(db, song, update.Album.NewValue, cancellationToken);
        }

        if (update.Artists is not null)
        {
            var artists = update.Artists.NewValue ?? [];
            if (artists.Count == 0)
            {
                throw new ValidationException("Song must have at least one artist");
            }
            await UpdateArtistsAsync(db, song, artists, cancellationToken);
        }

        if (update.Genres is not null)
        {
            await UpdateGenresAsync(db, song, update.Genres.NewValue ?? [], cancellationToken);
        }

        song.ModifiedAt = DateTime.UtcNow;
        song.Label = SongLabelBuilder.Build(song);
    }

    private async Task RemoveCoverAsync(MusicDbContext db, Song song)
    {
        if (song.Cover is not null)
        {
            var artwork = song.Cover;
            song.Cover = null;
            song.CoverId = null;
            await TryDeleteArtworkAsync(db, artwork);
        }
    }

    private async Task TryDeleteArtworkAsync(MusicDbContext db, Artwork artwork)
    {
        var isUsedBySong = await db.Songs.AnyAsync(s => s.CoverId == artwork.Id);
        var isUsedByAlbum = await db.Albums.AnyAsync(a => a.CoverId == artwork.Id);
        var isUsedByArtistPhoto = await db.Artists.AnyAsync(a => a.PhotoId == artwork.Id);
        var isUsedByArtistBackground = await db.Artists.AnyAsync(a => a.BackgroundId == artwork.Id);

        if (!isUsedBySong && !isUsedByAlbum && !isUsedByArtistPhoto && !isUsedByArtistBackground)
        {
            db.Artworks.Remove(artwork);
        }
    }

    private async Task UpdateCoverAsync(MusicDbContext db, Song song, string coverDataUrl,
        CancellationToken cancellationToken)
    {
        var newImageBuffer = await ImageBuffer.FromStringAsync(coverDataUrl, cancellationToken);
        var newSize = newImageBuffer.Size;

        if (song.Cover != null && song.Cover.Data.AsSpan().SequenceEqual(newImageBuffer.Data))
        {
            return;
        }

        var oldArtwork = song.Cover;
        song.Cover = new Artwork
        {
            Data = newImageBuffer.Data,
            MimeType = newImageBuffer.MimeType,
            Width = newSize.Width,
            Height = newSize.Height,
        };
        song.CoverId = null;
        await db.AddAsync(song.Cover, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        song.CoverId = song.Cover.Id;

        if (oldArtwork is not null)
        {
            await TryDeleteArtworkAsync(db, oldArtwork);
        }
    }

    private async Task UpdateAlbumAsync(MusicDbContext db, Song song, AlbumRef? albumRef, CancellationToken cancellationToken)
    {
        if (albumRef is null)
        {
            throw new ValidationException("Song must belong to an album");
        }

        Album? album = null;

        if (albumRef.Id.HasValue)
        {
            album = await db.Albums.FindAsync([albumRef.Id.Value], cancellationToken);
            if (album is null)
            {
                throw new ValidationException($"Album with ID {albumRef.Id.Value} not found");
            }
        }
        else if (!string.IsNullOrEmpty(albumRef.Name))
        {
            album = await db.Albums
                .FirstOrDefaultAsync(a => a.Name == albumRef.Name && a.OwnerId == song.OwnerId, cancellationToken);

            if (album == null)
            {
                Artist? albumArtist = null;

                if (!string.IsNullOrEmpty(albumRef.ArtistName))
                {
                    albumArtist = await GetOrCreateArtistAsync(db, albumRef.ArtistName, song.OwnerId, cancellationToken);
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
                        albumArtist = await GetOrCreateArtistAsync(db, "Unknown Artist", song.OwnerId, cancellationToken);
                    }
                }

                album = new Album
                {
                    Name = albumRef.Name,
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
        else
        {
            throw new ValidationException("Album reference must have either Id or Name");
        }

        song.Album = album;
        song.AlbumId = album.Id;
    }

    private async Task UpdateArtistsAsync(MusicDbContext db, Song song, List<ArtistRef> artistRefs,
        CancellationToken cancellationToken)
    {
        var artists = new List<Artist>();

        foreach (var artistRef in artistRefs)
        {
            Artist? artist = null;

            if (artistRef.Id.HasValue)
            {
                artist = await db.Artists.FindAsync([artistRef.Id.Value], cancellationToken);
            }
            else if (!string.IsNullOrEmpty(artistRef.Name))
            {
                artist = await GetOrCreateArtistAsync(db, artistRef.Name, song.OwnerId, cancellationToken);
            }

            if (artist is not null && !artists.Any(a => a.Id == artist.Id))
            {
                artists.Add(artist);
            }
        }

        if (artists.Count == 0)
        {
            throw new ValidationException("Song must have at least one valid artist");
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

    private async Task UpdateGenresAsync(MusicDbContext db, Song song, List<GenreRef> genreRefs,
        CancellationToken cancellationToken)
    {
        var genres = new List<Genre>();

        foreach (var genreRef in genreRefs)
        {
            Genre? genre = null;

            if (genreRef.Id.HasValue)
            {
                genre = await db.Genres.FindAsync([genreRef.Id.Value], cancellationToken);
            }
            else if (!string.IsNullOrEmpty(genreRef.Name))
            {
                genre = await GetOrCreateGenreAsync(db, genreRef.Name, song.OwnerId, cancellationToken);
            }

            if (genre is not null && !genres.Any(g => g.Id == genre.Id))
            {
                genres.Add(genre);
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

        song.Label = SongLabelBuilder.Build(song);

        var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
        song.Checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, song.RepositoryPath);
        song.ChecksumAlgorithm = checksumAlgorithm.GetType().Name;
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
