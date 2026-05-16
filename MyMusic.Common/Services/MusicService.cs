using System.IO.Abstractions;
using DotNext.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Targets;

namespace MyMusic.Common.Services;

public class MusicService(
    IFileSystem fileSystem,
    IOptions<Config> config,
    ISongMergeService songMergeService,
    ILogger<MusicService> logger)
    : IMusicService
{
    public const string MusicIgnoreFile = ".musicignore";

    /// <summary>
    ///     Repository management actions involve creating, updating, or removing songs from the repository, as well as adding,
    ///     updating or removing existing songs from a repository.
    ///     To prevent concurrent edits (which may result in duplicated songs or strange things like that) we have this lock
    ///     acting as a mutex.
    ///     Ideally we could only lock operations for the specific repository we are working with, but for now, we will not
    ///     delve into that extra complexity.
    /// </summary>
    private readonly AsyncReaderWriterLock _repositoryManagementLock = new();

    #region Device

    /// <summary>
    ///     Creates a new music device, associated with the given repositoryId.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="name"></param>
    /// <param name="ownerId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Device> CreateDevice(MusicDbContext db, string name, long ownerId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([ownerId], cancellationToken);

        if (user == null)
        {
            throw new Exception($"User not found with id {ownerId}");
        }

        var device = new Device
        {
            Name = name,
            Owner = user,
        };


        await db.AddAsync(device, cancellationToken);

        return device;
    }

    /// <summary>
    ///     Adds a music to a device (if it is not added already)
    /// </summary>
    /// <param name="db"></param>
    /// <param name="deviceId"></param>
    /// <param name="song"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task AddSongsToDevice(MusicDbContext db, long deviceId, Song song,
        CancellationToken cancellationToken = default)
    {
        var device = await db.Devices.FindAsync([deviceId], cancellationToken);

        if (device == null)
        {
            throw new Exception($"Device not found with id {deviceId}");
        }

        var songDevice = await db.SongDevices.FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == song.Id,
            cancellationToken);

        var namingStrategy = new TemplateNamingStrategy(
            device.NamingTemplate ?? config.Value.DefaultNamingTemplate);

        var naming = new NamingMetadata { Extension = Path.GetExtension(song.RepositoryPath) };

        if (songDevice == null)
        {
            songDevice = new SongDevice
            {
                DeviceId = deviceId,
                SongId = song.Id,
                SyncAction = SongSyncAction.Download,
                DevicePath = namingStrategy.Generate(EntityConverter.ToSong(song), naming),
                AddedAt = DateTime.Now,
            };

            await db.AddAsync(songDevice, cancellationToken);
        }
        else if (songDevice.SyncAction == SongSyncAction.Remove)
        {
            songDevice.SyncAction = null;
            songDevice.DevicePath = namingStrategy.Generate(EntityConverter.ToSong(song), naming);

            db.Update(songDevice);
        }
    }

    /// <summary>
    ///     Removes a music from a device (if it is added already)
    /// </summary>
    /// <param name="db"></param>
    /// <param name="deviceId"></param>
    /// <param name="song"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task RemoveSongsToDevice(MusicDbContext db, long deviceId, Song song,
        CancellationToken cancellationToken = default)
    {
        var songDevice = await db.SongDevices.FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == song.Id,
            cancellationToken);

        if (songDevice != null)
        {
            songDevice.SyncAction = SongSyncAction.Remove;
            db.Update(songDevice);
        }
    }

    #endregion Repository

    #region Synchronization

    /// <summary>
    ///     Returns a dictionary with the list of songs matching the checksums provided in the list.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="userId"></param>
    /// <param name="checksums"></param>
    /// <param name="checksumAlgorithm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Dictionary<string, Song>> FindUserSongsByChecksum(MusicDbContext db, long userId,
        List<string> checksums, string checksumAlgorithm, CancellationToken cancellationToken = default)
    {
        using (await _repositoryManagementLock.AcquireReadLockAsync(cancellationToken))
        {
            var songs = await db.Songs.Where(song => song.OwnerId == userId &&
                                                     song.ChecksumAlgorithm == checksumAlgorithm &&
                                                     checksums.Contains(song.Checksum)).ToListAsync(cancellationToken);

            return songs.ToDictionary(s => s.Checksum);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="db"></param>
    /// <param name="job"></param>
    /// <param name="userId"></param>
    /// <param name="rootSourceFolder"></param>
    /// <param name="deviceIds"></param>
    /// <param name="searchOption"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="duplicatesStrategy"></param>
    /// <returns></returns>
    public async Task ImportRepositorySongs(MusicDbContext db, MusicImportJob job, long userId, string rootSourceFolder,
        IList<long>? deviceIds = null,
        DuplicateSongsHandlingStrategy duplicatesStrategy = DuplicateSongsHandlingStrategy.SkipIdentical,
        SearchOption searchOption = SearchOption.AllDirectories, CancellationToken cancellationToken = default)
    {
        var importedSongs = new List<SongImportMetadata>();

        var sourceFoldersQueue = new Stack<string>();
        sourceFoldersQueue.Push(rootSourceFolder);

        while (sourceFoldersQueue.Count > 0)
        {
            var sourceFolder = sourceFoldersQueue.Pop();

            // Check if this folder should be ignored. If so, skip it completely
            if (fileSystem.File.Exists(fileSystem.Path.Combine(sourceFolder, MusicIgnoreFile)))
            {
                continue;
            }

            var files = fileSystem.Directory.GetFiles(sourceFolder);

            foreach (var filePath in files)
            {
                var extension = fileSystem.Path.GetExtension(filePath);

                if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".m4a", StringComparison.OrdinalIgnoreCase))
                {
                    importedSongs.Add(new SongImportMetadata(filePath, fileSystem.File.GetCreationTimeUtc(filePath),
                        fileSystem.File.GetLastWriteTimeUtc(filePath)));
                }
            }

            // If this function was called to scan all subdirectories as well
            if (searchOption == SearchOption.AllDirectories)
            {
                var subFolders = fileSystem.Directory.GetDirectories(sourceFolder);

                foreach (var subFolder in subFolders)
                {
                    sourceFoldersQueue.Push(subFolder);
                }
            }
        }

        await ImportRepositorySongs(db, job, userId, importedSongs, deviceIds, duplicatesStrategy, cancellationToken);
    }

    /// <summary>
    /// </summary>
    /// <param name="db"></param>
    /// <param name="job"></param>
    /// <param name="userId"></param>
    /// <param name="importSongsMetadataList"></param>
    /// <param name="deviceIds"></param>
    /// <param name="duplicatesStrategy"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task ImportRepositorySongs(MusicDbContext db, MusicImportJob job, long userId,
        IEnumerable<SongImportMetadata> importSongsMetadataList, IList<long>? deviceIds = null,
        DuplicateSongsHandlingStrategy duplicatesStrategy = DuplicateSongsHandlingStrategy.SkipIdentical,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Acquiring lock to begin importing songs into repository Id {RepositoryId}", userId);

        using (await _repositoryManagementLock.AcquireWriteLockAsync(cancellationToken))
        {
            logger.LogDebug("  >> Lock acquired");

            var repo = new UserMusicService(db, userId);

            var user = (await db.Users.FindAsync([userId], cancellationToken))!;
            var repositoryDevices =
                await db.Devices.Where(device => device.Owner.Id == userId).ToListAsync(cancellationToken);
            var repositoryDeviceIds = repositoryDevices.Select(device => device.Id).ToHashSet();

            // Check if all device ids passed as arguments to this function are associated with the repository id also passed to this function
            if (deviceIds != null)
            {
                foreach (var deviceId in deviceIds)
                {
                    if (!repositoryDeviceIds.Contains(deviceId))
                    {
                        throw new Exception(
                            $"Repository {userId} does not contain device {deviceId}, as such, cannot import songs into it.");
                    }
                }
            }

            // Create the objects representing all the songs
            var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
            var checksumAlgorithmName = checksumAlgorithm.GetType().Name;

            foreach (var importSongMetadata in importSongsMetadataList)
            {
                await using var dbTrans = await db.Database.BeginTransactionAsync(cancellationToken);

                logger.LogDebug("Importing song from file {SongFilePath}", importSongMetadata.SourceFilePath);

                SongMetadata? metadata = null;

                try
                {
                    var sourceFile = new FileTarget(fileSystem) { FilePath = importSongMetadata.SourceFilePath };
                    var targetFile = new FileTarget(fileSystem)
                        { Folder = fileSystem.Path.Join(config.Value.MusicRepositoryPath, user.Username) };

                    var naming = NamingMetadata.FromPath(sourceFile.FilePath);

                    metadata = await sourceFile.ReadMetadata(cancellationToken);

                    // Get the length of the file
                    var duration = metadata.Duration;

                    logger.LogDebug("  >> Metadata read: {Song}", metadata.FullLabel);

                    #region Metadata Validations

                    if (string.IsNullOrEmpty(metadata.Title))
                    {
                        job.AddSkipReason(new MissingTitleSkipReason(importSongMetadata.SourceFilePath));
                        continue;
                    }

                    if (metadata.Album is null)
                    {
                        job.AddSkipReason(new MissingAlbumSkipReason(importSongMetadata.SourceFilePath));
                        continue;
                    }

                    if (string.IsNullOrEmpty(metadata.Album.Name))
                    {
                        job.AddSkipReason(new MissingAlbumNameSkipReason(importSongMetadata.SourceFilePath));
                        continue;
                    }

                    if (metadata.Album.Artist is null)
                    {
                        job.AddSkipReason(new MissingAlbumArtistSkipReason(importSongMetadata.SourceFilePath));
                        continue;
                    }

                    if (string.IsNullOrEmpty(metadata.Album.Artist.Name))
                    {
                        job.AddSkipReason(new MissingAlbumArtistNameSkipReason(importSongMetadata.SourceFilePath));
                        continue;
                    }

                    #endregion

                    var checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, sourceFile.FilePath);

                    logger.LogDebug("  >> Checksum calculated: {Checksum} ({Algorithm}) for file {FilePath}",
                        checksum, checksumAlgorithmName, importSongMetadata.SourceFilePath);

                    var song = await repo.GetSongByChecksum(checksum, checksumAlgorithmName, cancellationToken);

                    if (song is not null)
                    {
                        logger.LogDebug("  >> Found existing song by checksum: SongId={SongId}, Title='{Title}', Album='{Album}', RepositoryPath='{RepositoryPath}'",
                            song.Id, song.Title, song.Album?.Name, song.RepositoryPath);
                    }
                    else
                    {
                        logger.LogDebug("  >> No existing song found with matching checksum");
                    }

                    if (song is not null &&
                        importSongMetadata.SongId.HasValue &&
                        importSongMetadata.SongId.Value != song.Id)
                    {
                        var existingSong = await db.Songs.FindAsync([importSongMetadata.SongId.Value], cancellationToken);
                        logger.LogDebug("  >> MERGE TRIGGERED: Existing song found by checksum (Id={ExistingSongId}, Title='{ExistingTitle}') differs from uploaded song (SongId={UploadedSongId}, Title='{UploadedTitle}'). Checksum={Checksum}",
                            song.Id, song.Title, importSongMetadata.SongId.Value, existingSong?.Title ?? "(not found)", checksum);

                        var mergeResult = await songMergeService.MergeSongsAsync(
                            db,
                            song.Id,
                            importSongMetadata.SongId.Value,
                            cancellationToken);

                        if (!mergeResult.Success)
                        {
                            logger.LogError("  >> MERGE FAILED: {ErrorMessage}", mergeResult.ErrorMessage);
                            job.AddException(new Exception($"Failed to merge songs: {mergeResult.ErrorMessage}"));
                            continue;
                        }

                        logger.LogDebug("  >> MERGE SUCCEEDED: Merged song {MergeFromSongId} into {KeepSongId}", importSongMetadata.SongId.Value, song.Id);

                        await dbTrans.CommitAsync(cancellationToken);

                        job.AddSongMapping(importSongMetadata, song);
                        continue;
                    }

                    if (song is not null &&
                        File.Exists(song.RepositoryPath) &&
                        (duplicatesStrategy == DuplicateSongsHandlingStrategy.Skip ||
                         duplicatesStrategy == DuplicateSongsHandlingStrategy.SkipIdentical))
                    {
                        logger.LogDebug("  >> SKIPPING: Duplicate checksum found. SongId={SongId}, RepositoryPath='{Path}', Strategy={Strategy}",
                            song.Id, song.RepositoryPath, duplicatesStrategy);
                        job.AddSkipReason(new DuplicateChecksumSkipReason(importSongMetadata.SourceFilePath,
                            metadata.FullLabel, checksum, checksumAlgorithmName, song.Label, song.Id));
                        job.AddSongMapping(importSongMetadata, song);
                        continue;
                    }

                    targetFile.EnsureFilePath(metadata, naming);

                    song ??= await repo.GetSongByPath(targetFile.FilePath!, cancellationToken);

                    if (song is not null)
                    {
                        logger.LogDebug("  >> Found existing song by repository path: SongId={SongId}, Path='{Path}'",
                            song.Id, targetFile.FilePath);
                    }

                    if (song is null && importSongMetadata.SongId.HasValue)
                    {
                        logger.LogDebug("  >> Loading song by SongId={SongId} from import metadata", importSongMetadata.SongId.Value);
                        song = await db.Songs
                            .Include(s => s.Devices)
                            .FirstOrDefaultAsync(s => s.Id == importSongMetadata.SongId.Value, cancellationToken);
                    }

                    if (song is not null &&
                        File.Exists(song.RepositoryPath) &&
                        duplicatesStrategy == DuplicateSongsHandlingStrategy.Skip)
                    {
                        logger.LogDebug("  >> SKIPPING: File exists at repository path with Skip strategy. SongId={SongId}, Path='{Path}'",
                            song.Id, song.RepositoryPath);
                        job.AddSkipReason(new DuplicateFilePathSkipReason(importSongMetadata.SourceFilePath,
                            metadata.FullLabel, targetFile.FilePath!, song.Label, song.Id));
                        job.AddSongMapping(importSongMetadata, song);
                        continue;
                    }

                    if (song is not null)
                    {
                        logger.LogDebug("  >> UPDATING existing song: SongId={SongId}, Title='{OldTitle}' -> '{NewTitle}', Album='{OldAlbum}' -> '{NewAlbum}'",
                            song.Id, song.Title, metadata.Title, song.Album?.Name, metadata.Album?.Name);

                        song = await db.Songs
                            .Include(s => s.Artists)
                            .Include(s => s.Genres)
                            .Include(s => s.Cover)
                            .Include(s => s.Album)
                            .ThenInclude(a => a!.Artist)
                            .Include(s => s.Devices)
                            .FirstAsync(s => s.Id == song.Id, cancellationToken);
                    }
                    else
                    {
                        logger.LogDebug("  >> CREATING new song: Title='{Title}', Album='{Album}', Artists=[{Artists}]",
                            metadata.Title, metadata.Album?.Name, string.Join(", ", metadata.Artists?.Select(a => a.Name) ?? []));
                    }

                    ImageBuffer? cover = null;

                    if (metadata.Album.CoverArt?.Biggest is not null)
                    {
                        var biggestCoverArt = metadata.Album.CoverArt.Biggest!;

                        if (biggestCoverArt.Length > 500)
                        {
                            logger.LogDebug("Downloading cover {CoverUrl}", biggestCoverArt.Substring(0, 1000));
                        }
                        else
                        {
                            logger.LogDebug("Downloading cover {CoverUrl}", biggestCoverArt);
                        }

                        cover = await ImageBuffer.FromStringAsync(biggestCoverArt, cancellationToken);
                    }

                    Album? songAlbum = null;
                    var songGenres = new List<SongGenre>();
                    var songDevices = new List<SongDevice>();
                    var songArtists = new List<SongArtist>();

                    #region Album

                    // We are updating an existing song, and the Album remains the same
                    if (song?.Album?.Name == metadata.Album.Name &&
                        song?.Album?.Artist?.Name == metadata.Album.Artist.Name)
                    {
                        songAlbum = song.Album;
                    }
                    // We are updating an existing song, and the Album has changed
                    else if (song?.Album is not null)
                    {
                        song.Album.SongsCount -= 1;

                        db.Update(song.Album);
                    }

                    // If we are updating an existing song, but the album/album artist changed, or
                    // if we are creating a new song
                    if (songAlbum is null)
                    {
                        // Try and find an existing album for this Artist with the given name
                        songAlbum = await repo.GetArtistAlbum(metadata.Album.Artist.Name, metadata.Album.Name,
                            cancellationToken);

                        if (songAlbum is not null)
                        {
                            songAlbum.SongsCount += 1;

                            db.Update(songAlbum);
                        }
                    }

                    // If no Album with this name belonging to this Artist exists on the database yet
                    if (songAlbum is null)
                    {
                        var songAlbumArtist = (await repo.GetArtists(metadata.Album.Artist.Name, cancellationToken))
                            .FirstOrDefault();

                        if (songAlbumArtist is null)
                        {
                            songAlbumArtist = new Artist
                            {
                                Name = metadata.Album.Artist.Name, OwnerId = userId, AlbumsCount = 1, SongsCount = 0,
                                CreatedAt = DateTime.UtcNow,
                            };

                            await db.AddAsync(songAlbumArtist, cancellationToken);
                        }
                        else
                        {
                            songAlbumArtist.AlbumsCount += 1;
                            db.Update(songAlbumArtist);
                        }

                        songAlbum = new Album
                        {
                            Name = metadata.Album.Name, Artist = songAlbumArtist, OwnerId = userId, SongsCount = 1,
                            CreatedAt = DateTime.UtcNow,
                        };
                        await db.AddAsync(songAlbum, cancellationToken);
                    }

                    #endregion Album

                    // TODO Find a better way to re-use the album artist, if needed
                    await db.SaveChangesAsync(cancellationToken);

                    #region Artists

                    if (metadata.Artists is not null)
                    {
                        foreach (var artist in metadata.Artists.Distinct())
                        {
                            var songArtist = (await repo.GetArtists(artist.Name, cancellationToken)).FirstOrDefault();

                            if (songArtist is null)
                            {
                                songArtist = new Artist
                                {
                                    Name = artist.Name, Owner = user, AlbumsCount = 0, SongsCount = 0,
                                    CreatedAt = DateTime.UtcNow,
                                };

                                await db.AddAsync(songArtist, cancellationToken);
                            }

                            // Add the song artist that already belonged to the song (if any) or create a new one
                            songArtists.Add(
                                song?.Artists?.FirstOrDefault(sa => sa.ArtistId == songArtist.Id)
                                ??
                                new SongArtist { SongId = 0, Artist = songArtist }
                            );
                        }
                    }

                    #endregion Artists

                    #region Genres

                    if (metadata.Genres is not null)
                    {
                        foreach (var genre in metadata.Genres.Distinct())
                        {
                            var songGenre = await repo.GetGenre(genre, cancellationToken);

                            if (songGenre is null)
                            {
                                songGenre = new Genre { Name = genre, OwnerId = userId };

                                await db.AddAsync(songGenre, cancellationToken);
                            }

                            // Add the song genre that already belonged to the song (if any) or create a new one
                            songGenres.Add(
                                song?.Genres?.FirstOrDefault(sa => sa.GenreId == songGenre.Id)
                                ??
                                new SongGenre { SongId = 0, Genre = songGenre }
                            );
                        }
                    }

                    #endregion Genres

                    var size = fileSystem.FileInfo.New(sourceFile.FilePath).Length;

                    await using (var sourceStream = sourceFile.Read())
                    {
                        await targetFile.Save(sourceStream, metadata, naming, cancellationToken);
                    }

                    job.AddFileMapping(sourceFile.FilePath!, targetFile.FilePath!);

                    await targetFile.SetTimestamps(importSongMetadata.CreatedAt, importSongMetadata.ModifiedAt,
                        cancellationToken);

                    if (song is null)
                    {
                        // Use Activator to avoid having to specify all the required fields manually
                        song = Activator.CreateInstance<Song>();

                        // Timestamps
                        song.CreatedAt = importSongMetadata.CreatedAt.ToUniversalTime();
                        song.ModifiedAt = importSongMetadata.ModifiedAt.ToUniversalTime();
                        song.AddedAt = DateTime.UtcNow;
                        // Repository Id
                        song.OwnerId = userId;
                        song.RepositoryPath = targetFile.FilePath!;

                        await db.AddAsync(song, cancellationToken);

                        foreach (var sg in songGenres)
                        {
                            sg.Song = song;

                            await db.AddAsync(sg, cancellationToken);
                        }

                        foreach (var sa in songArtists)
                        {
                            sa.Song = song;

                            await db.AddAsync(sa, cancellationToken);
                        }
                    }
                    else
                    {
                        foreach (var sg in songGenres.Where(sg => sg.SongId == 0))
                        {
                            sg.Song = song;

                            await db.AddAsync(sg, cancellationToken);
                        }

                        foreach (var sa in songArtists.Where(sa => sa.SongId == 0))
                        {
                            sa.Song = song;

                            await db.AddAsync(sa, cancellationToken);
                        }
                    }

                    var artistsDiff = ReferencesDiff.From(song.Artists, songArtists, sa => sa.Id);
                    var genresDiff = ReferencesDiff.From(song.Genres, songGenres, sa => sa.Id);

                    if (artistsDiff.Removed.Count != 0)
                    {
                        foreach (var artist in artistsDiff.Removed)
                        {
                            artist.Artist.SongsCount -= 1;
                        }

                        db.UpdateRange(artistsDiff.Removed.Select(a => a.Artist));
                        db.RemoveRange(artistsDiff.Removed);
                    }

                    if (genresDiff.Removed.Count != 0)
                    {
                        db.RemoveRange(genresDiff.Removed);
                    }

                    song.Title = metadata.Title;
                    song.Label = metadata.FullLabel;
                    song.Year = metadata.Year;
                    song.Lyrics = metadata.Lyrics;
                    song.Explicit = metadata.Explicit;
                    song.Size = size;
                    song.Rating = metadata.Rating;
                    song.Track = metadata.Track;
                    song.Duration = duration;
                    song.Bitrate = metadata.Bitrate;
                    song.Checksum = checksum;
                    song.ChecksumAlgorithm = checksumAlgorithmName;
                    song.Album = songAlbum;
                    song.Genres = songGenres;
                    song.Devices = song.Devices is { Count: > 0 } ? song.Devices : songDevices;
                    song.Artists = songArtists;
                    song.ModifiedAt = importSongMetadata.CreatedAt.ToUniversalTime();

                    if (cover is not null)
                    {
                        var songCover = song.Cover;

                        if (songCover is null)
                        {
                            songCover = Activator.CreateInstance<Artwork>();

                            await db.AddAsync(songCover, cancellationToken);

                            song.Cover = songCover;
                        }

                        var coverDimensions = cover.Size;

                        songCover.Data = cover.Data;
                        songCover.MimeType = cover.MimeType;
                        songCover.Width = coverDimensions.Width;
                        songCover.Height = coverDimensions.Height;
                    }
                    else if (song.Cover is not null)
                    {
                        // If the old song had a cover, and the new one doesn't, delete it
                        db.Remove(song.Cover);

                        song.Cover = null;
                    }

                    //songsToCreate.Add(song);

                    await db.SaveChangesAsync(cancellationToken);
                    await dbTrans.CommitAsync(cancellationToken);

                    job.AddSongMapping(importSongMetadata, song);
                }
                catch (TaskCanceledException)
                {
                    await dbTrans.RollbackAsync(cancellationToken);

                    throw;
                }
                catch (Exception ex)
                {
                    job.AddException(new Exception(
                        $"Failed to import song {metadata?.FullLabel ?? "(undefined)"} from file {importSongMetadata.SourceFilePath}",
                        ex));

                    logger.LogError(ex, "Failed to import song {SongLabel} from file {File}",
                        metadata?.FullLabel ?? "(undefined)", importSongMetadata.SourceFilePath);

                    await dbTrans.RollbackAsync(cancellationToken);
                }
            }
        }

        logger.LogDebug("  >> Lock released");
    }

    #endregion
}

public enum DuplicateSongsHandlingStrategy
{
    /// <summary>
    ///     Do not import the song if it already exists in the repository (same checksum or same file path)
    /// </summary>
    Skip,

    /// <summary>
    ///     Do not import the song if it already exists in the repository as an exact match (same checksum)
    /// </summary>
    SkipIdentical,

    /// <summary>
    ///     Replace the existing song, if it already exists in the repository (same checksum or same file path)
    /// </summary>
    Overwrite,
}
