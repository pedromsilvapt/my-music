using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Services.Songs;
using MyMusic.Common.Targets;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services;

public interface ISongUpdateService
{
    Task<SongUpdateResult> UpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default);

    Task<BatchUpdateResult> BatchUpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates several songs inside the transaction the caller already began on <paramref name="db"/>, with the
    ///     file changes going to the caller's <paramref name="files"/>. Nothing is committed here, and the first
    ///     failing song throws: the caller rolls back, undoing every song's rows and files.
    /// </summary>
    /// <remarks>
    ///     No artist or album advisory locks are taken. The caller must already hold, from a single
    ///     <see cref="IAdvisoryLockService.AcquireTransactionLocksAsync"/> call, the
    ///     <see cref="AlbumArtistLockKeys"/> of every artist and album name the updates involve.
    /// </remarks>
    Task UpdateSongsAsync(MusicDbContext db, IFileTransaction files,
        IEnumerable<(long SongId, SongUpdateModel Update)> updates, SongUpdateOptions? options = null,
        CancellationToken cancellationToken = default);
}

public class SongUpdateService(
    ISongFileUpdateService songFileUpdate,
    IFileTransactionService fileTransactions,
    IAdvisoryLockService advisoryLocks,
    IAlbumUpsertService albumUpsert,
    IAlbumDeleteService albumDelete,
    IArtistDeleteService artistDelete,
    IArtworkDeleteService artworkDelete,
    ILogger<SongUpdateService> logger) : ISongUpdateService
{

    public async Task<SongUpdateResult> UpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default)
    {
        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);

        var song = await UpdateSongCoreAsync(db, files, songId, update, locks, SongUpdateOptions.Default,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return MapToResult(song);
    }

    public async Task<BatchUpdateResult> BatchUpdateSong(MusicDbContext db, long songId, SongUpdateModel update,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Songs.AnyAsync(s => s.Id == songId, cancellationToken))
        {
            return new BatchUpdateResult
            {
                Id = songId,
                Success = false,
                Error = $"Song not found with id {songId}",
            };
        }

        // Declared first, so the locks are only released once the transaction has ended
        await using var locks = new AdvisoryLockHolder();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var files = fileTransactions.Begin(db);
        try
        {
            var song = await UpdateSongCoreAsync(db, files, songId, update, locks, SongUpdateOptions.Default,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

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

    public async Task UpdateSongsAsync(MusicDbContext db, IFileTransaction files,
        IEnumerable<(long SongId, SongUpdateModel Update)> updates, SongUpdateOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Updating songs in an ambient transaction requires the caller to have begun one.");
        }

        foreach (var (songId, update) in updates)
        {
            await UpdateSongCoreAsync(db, files, songId, update, locks: null, options ?? SongUpdateOptions.Default,
                cancellationToken);
        }
    }

    /// <summary>
    ///     Updates one song inside the current transaction of <paramref name="db"/>, without committing it: applies
    ///     the update, deletes the album and artists it left unused, rewrites the file and moves it to its new path.
    /// </summary>
    /// <param name="locks">
    ///     Receives the artist and album locks the update takes; <c>null</c> when the caller already holds them.
    /// </param>
    private async Task<Song> UpdateSongCoreAsync(MusicDbContext db, IFileTransaction files, long songId,
        SongUpdateModel update, AdvisoryLockHolder? locks, SongUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var song = await LoadSongAsync(db, songId, cancellationToken);

        if (song == null)
        {
            throw new Exception($"Song not found with id {songId}");
        }

        var oldTitle = song.Title;
        var oldChecksum = song.Checksum;
        var oldAlbumId = song.AlbumId;
        var oldArtistNames = song.Artists?.Select(a => a.Artist?.Name).ToList();
        var oldArtistIds = GetAlbumAndSongArtistIds(song);

        await ApplyUpdatesAsync(db, song, update, locks, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await DeleteUnusedAlbumAndArtistsAsync(db, song, oldAlbumId, oldArtistIds, options, cancellationToken);

        // Reload the song to ensure navigation properties are correctly loaded after updates
        // This is necessary because EF Core might not preserve navigation properties after SaveChanges
        db.Entry(song).State = EntityState.Detached;
        song = (await LoadSongAsync(db, songId, cancellationToken))!;

        var previousChecksum = song.Checksum;
        var fileUpdate = await songFileUpdate.UpdateAsync(db, files, song,
            () => BuildSongUpdateReason(song, update, oldChecksum, oldTitle, oldAlbumId, oldArtistNames),
            cancellationToken);

        logger.LogInformation("Song {SongId} update: previousChecksum={PreviousChecksum}, newChecksum={NewChecksum}",
            songId, previousChecksum, song.Checksum);

        if (fileUpdate.ChecksumChanged)
        {
            logger.LogInformation("Marked devices for download for song {SongId}, saving changes", songId);
        }
        else
        {
            logger.LogWarning("No changes requiring device update for song {SongId}", songId);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved changes for song {SongId}", songId);

        // The file only moves once the (owner, path) unique index lets the song claim its new path, and moves back
        // if the transaction does not commit, so it always stays where the database says it is
        if (fileUpdate.PreviousPath is not null)
        {
            await files.MoveAsync(fileUpdate.PreviousPath, song.RepositoryPath, cancellationToken);
        }

        return song;
    }

    private async Task<Song?> LoadSongAsync(MusicDbContext db, long songId, CancellationToken cancellationToken)
    {
        return await db.Songs
            .Where(s => s.Id == songId)
            .Include(s => s.Owner)
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres)
            .ThenInclude(sg => sg.Genre)
            .Include(s => s.Cover)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task ApplyUpdatesAsync(MusicDbContext db, Song song, SongUpdateModel update,
        AdvisoryLockHolder? locks, CancellationToken cancellationToken)
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

        if (update.Album is not null || update.Artists is not null)
        {
            await UpdateAlbumAndArtistsAsync(db, song, update, locks, cancellationToken);
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

    /// <summary>
    ///     Applies the album and artists updates together, since they constrain each other: the album is found, or
    ///     created, among the albums of its album artist, who must be one of the song's artists.
    /// </summary>
    private async Task UpdateAlbumAndArtistsAsync(MusicDbContext db, Song song, SongUpdateModel update,
        AdvisoryLockHolder? locks, CancellationToken cancellationToken)
    {
        var artistRefs = update.Artists?.NewValue;
        if (update.Artists is not null && artistRefs is not { Count: > 0 })
        {
            throw new ValidationException("Song must have at least one artist");
        }

        var albumRef = update.Album is null ? null : update.Album.NewValue ?? new AlbumRef();

        // An album referenced by id is that exact album, along with its own album artist
        Album? albumById = null;
        if (albumRef?.Id is { } albumId)
        {
            albumById = await db.Albums
                .Include(a => a.Artist)
                .FirstOrDefaultAsync(a => a.Id == albumId && a.OwnerId == song.OwnerId, cancellationToken)
                ?? throw new ValidationException($"Album with ID {albumId} not found");
            albumRef = new AlbumRef(albumById.Name, new ArtistRef(albumById.ArtistId));
        }

        var albumArtistRef = IsEmpty(albumRef?.Artist) ? null : albumRef!.Artist;

        // Artists referenced by id are loaded first: their names are part of the lock keys
        var artistsById = new Dictionary<long, Artist>();
        foreach (var artistRef in (artistRefs ?? []).Append(albumArtistRef))
        {
            if (artistRef?.Id is { } artistId && !artistsById.ContainsKey(artistId)
                && await db.Artists.FindAsync([artistId], cancellationToken) is { } artist)
            {
                artistsById[artistId] = artist;
            }
        }

        if (albumArtistRef?.Id is { } albumArtistId && !artistsById.ContainsKey(albumArtistId))
        {
            throw new ValidationException($"Album artist with ID {albumArtistId} not found");
        }

        string? NameOf(ArtistRef artistRef) =>
            artistRef.Id is { } id ? artistsById.GetValueOrDefault(id)?.Name : artistRef.Name;

        var albumName = albumRef is null
            ? song.Album.Name
            : string.IsNullOrEmpty(albumRef.Name) ? Album.PlaceholderName : albumRef.Name;
        var albumArtistName = albumArtistRef is null ? song.Album.Artist.Name : NameOf(albumArtistRef)!;

        if (locks is not null)
        {
            // The same keys a song import takes, so neither can find-or-create the same artist or album concurrently
            var lockKeys = AlbumArtistLockKeys.Create(song.OwnerId,
                (artistRefs ?? []).Select(NameOf).Where(name => !string.IsNullOrEmpty(name))!,
                [(albumArtistName, albumName)]);

            locks.Handle = await advisoryLocks.AcquireTransactionLocksAsync(db, lockKeys, cancellationToken);
        }

        // Artist names are not unique: a name resolves to the same artist everywhere in this update, preferring an
        // artist the update also references by id
        var artistsByName = new Dictionary<string, Artist>();
        foreach (var artist in artistsById.Values)
        {
            artistsByName.TryAdd(artist.Name, artist);
        }

        async Task<Artist?> ResolveAsync(ArtistRef artistRef)
        {
            if (artistRef.Id is { } id)
            {
                return artistsById.GetValueOrDefault(id);
            }

            if (string.IsNullOrEmpty(artistRef.Name))
            {
                return null;
            }

            if (!artistsByName.TryGetValue(artistRef.Name, out var artist))
            {
                artist = await GetOrCreateArtistAsync(db, artistRef.Name, song.OwnerId, cancellationToken);
                artistsByName[artistRef.Name] = artist;
            }

            return artist;
        }

        var artists = song.Artists.Select(sa => sa.Artist).ToList();
        if (artistRefs is not null)
        {
            artists = [];
            foreach (var artistRef in artistRefs)
            {
                if (await ResolveAsync(artistRef) is { } artist && !artists.Contains(artist))
                {
                    artists.Add(artist);
                }
            }

            if (artists.Count == 0)
            {
                throw new ValidationException("Song must have at least one valid artist");
            }
        }

        var albumArtist = albumArtistRef is null ? song.Album.Artist : (await ResolveAsync(albumArtistRef))!;

        if (!artists.Contains(albumArtist))
        {
            throw new ValidationException(
                $"The album artist '{albumArtist.Name}' must be one of the song's artists.");
        }

        if (artistRefs is not null)
        {
            db.SongArtists.RemoveRange(song.Artists);

            song.Artists = artists.Select(a => new SongArtist
            {
                Song = song,
                SongId = song.Id,
                Artist = a,
                ArtistId = a.Id,
            }).ToList();
        }

        var album = albumById
                    ?? await albumUpsert.UpsertAsync(db, song.OwnerId, albumName, albumArtist, cancellationToken);
        song.Album = album;
        song.AlbumId = album.Id;
    }

    private static bool IsEmpty(ArtistRef? artistRef) =>
        artistRef is null || (artistRef.Id is null && string.IsNullOrEmpty(artistRef.Name));

    private static async Task<Artist> GetOrCreateArtistAsync(MusicDbContext db, string name, long ownerId,
        CancellationToken cancellationToken)
    {
        var artist = await db.Artists
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(a => a.Name == name && a.OwnerId == ownerId, cancellationToken);

        if (artist != null)
        {
            return artist;
        }

        artist = new Artist
        {
            Name = name,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };
        await db.AddAsync(artist, cancellationToken);

        return artist;
    }

    private static long[] GetAlbumAndSongArtistIds(Song song) =>
        song.Artists.Select(sa => sa.ArtistId).Append(song.Album.ArtistId).Distinct().ToArray();

    /// <summary>
    ///     Deletes the album and the artists the saved <paramref name="song"/> moved away from, when nothing else uses
    ///     them anymore, along with their artworks. The albums and artists <paramref name="options"/> keeps survive.
    /// </summary>
    private async Task DeleteUnusedAlbumAndArtistsAsync(MusicDbContext db, Song song, long oldAlbumId,
        long[] oldArtistIds, SongUpdateOptions options, CancellationToken cancellationToken)
    {
        long[] albumIds = oldAlbumId != song.AlbumId && !options.KeepAlbumIds.Contains(oldAlbumId)
            ? [oldAlbumId]
            : [];
        var artistIds = oldArtistIds
            .Except(GetAlbumAndSongArtistIds(song))
            .Except(options.KeepArtistIds)
            .ToArray();

        if (albumIds.Length == 0 && artistIds.Length == 0)
        {
            return;
        }

        var albumArtworkIds = await db.Albums
            .Where(a => albumIds.Contains(a.Id) && a.CoverId != null)
            .Select(a => a.CoverId!.Value)
            .ToListAsync(cancellationToken);
        var artistArtworks = await db.Artists
            .Where(a => artistIds.Contains(a.Id) && (a.PhotoId != null || a.BackgroundId != null))
            .Select(a => new { a.PhotoId, a.BackgroundId })
            .ToListAsync(cancellationToken);

        // Albums first: an artist is only unused once it has no albums left
        var deletedAlbumIds = await albumDelete.DeleteIfUnusedAsync(albumIds, cancellationToken);
        var deletedArtistIds = await artistDelete.DeleteIfUnusedAsync(artistIds, cancellationToken);

        if (deletedAlbumIds.Length == 0 && deletedArtistIds.Length == 0)
        {
            return;
        }

        var artworkIds = artistArtworks
            .SelectMany(a => new[] { a.PhotoId, a.BackgroundId })
            .OfType<long>()
            .Concat(albumArtworkIds)
            .Distinct()
            .ToArray();
        await artworkDelete.DeleteIfUnusedAsync(artworkIds, cancellationToken);

        // They were deleted directly in the database: stop tracking them, so later updates on this context cannot
        // pick them up again
        var deletedEntries = db.ChangeTracker.Entries()
            .Where(entry => entry.Entity is Album album && deletedAlbumIds.Contains(album.Id)
                            || entry.Entity is Artist artist && deletedArtistIds.Contains(artist.Id))
            .ToList();
        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Detached;
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

    private const int MaxReasonLength = 2048;

    private static string BuildSongUpdateReason(Song song, SongUpdateModel update, string previousChecksum,
        string oldTitle, long? oldAlbumId, List<string?>? oldArtistNames)
    {
        var changes = new List<string>();

        if (update.Title != null && oldTitle != song.Title)
        {
            changes.Add($"title '{oldTitle}' → '{song.Title}'");
        }

        if (song.Checksum != previousChecksum)
        {
            changes.Add($"checksum {previousChecksum} → {song.Checksum}");
        }

        if (update.Album != null && oldAlbumId != song.AlbumId)
        {
            changes.Add("album changed");
        }

        if (update.Artists != null)
        {
            var newArtistNames = song.Artists?.Select(a => a.Artist?.Name).ToList();
            if (oldArtistNames == null || newArtistNames == null
                || !oldArtistNames.SequenceEqual(newArtistNames))
            {
                changes.Add("artists changed");
            }
        }

        var reason = changes.Count > 0
            ? $"Song updated: {string.Join(", ", changes)}"
            : "Song updated";

        return reason.Length > MaxReasonLength
            ? string.Concat(reason.AsSpan(0, MaxReasonLength - 3), "...")
            : reason;
    }
}
