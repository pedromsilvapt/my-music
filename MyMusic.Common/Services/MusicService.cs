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
using MyMusic.Common.Utilities;

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
    public async Task<SongDevice> AddSongsToDevice(MusicDbContext db, long deviceId, Song song,
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
                SyncActionReason = "Song added to device",
                DevicePath = namingStrategy.Generate(EntityConverter.ToSong(song), naming),
                AddedAt = DateTime.UtcNow,
            };

            await db.AddAsync(songDevice, cancellationToken);
        }
        else if (songDevice.SyncAction == SongSyncAction.Remove)
        {
            songDevice.SyncAction = null;
            songDevice.SyncActionReason = null;
            songDevice.DevicePath = namingStrategy.Generate(EntityConverter.ToSong(song), naming);

            db.Update(songDevice);
        }

        return songDevice;
    }

    public async Task<SongDevice> AddSongsToDevice(MusicDbContext db, long deviceId, long songId, string devicePath,
        DateTime modifiedAt, CancellationToken cancellationToken = default)
    {
        var existing = await db.SongDevices
            .FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.DevicePath == devicePath, cancellationToken);

        if (existing != null)
        {
            logger.LogInformation("AddSongsToDevice: RETURNING EARLY for path={Path}, existing.LastSyncedModifiedAtTicks={LastSyncedTicks}", devicePath, existing.LastSyncedModifiedAt?.Ticks);
            return existing;
        }

        logger.LogInformation("AddSongsToDevice: ADDING new SongDevice for path={Path}, songId={SongId}, modifiedAtTicks={ModifiedAtTicks}", devicePath, songId, modifiedAt.Ticks);
        var songDevice = new SongDevice
        {
            DeviceId = deviceId,
            SongId = songId,
            DevicePath = devicePath,
            AddedAt = DateTime.UtcNow,
            LastSyncedModifiedAt = modifiedAt,
        };
        db.SongDevices.Add(songDevice);
        return songDevice;
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
            songDevice.SyncActionReason = "Song removed from device";
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
        DuplicateSongsHandlingStrategy duplicatesStrategy = DuplicateSongsHandlingStrategy.Skip,
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
        DuplicateSongsHandlingStrategy duplicatesStrategy = DuplicateSongsHandlingStrategy.Skip,
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

                    var naming = NamingMetadata.FromPath(importSongMetadata.OriginalFilePath ?? sourceFile.FilePath);

                    metadata = await sourceFile.ReadMetadata(cancellationToken);

                    // Get the length of the file
                    var duration = metadata.Duration;

                    logger.LogDebug("  >> Metadata read: {Song}", metadata.FullLabel);

                    #region Placeholder Values for Missing Metadata

                    // Use placeholder values for missing metadata fields when writing to the database.
                    // Do NOT mutate the metadata object — it is used for file operations (path generation,
                    // tag writing) and must reflect the actual file contents to preserve checksum integrity.

                    var effectiveTitle = string.IsNullOrEmpty(metadata.Title)
                        ? fileSystem.Path.GetFileNameWithoutExtension(importSongMetadata.OriginalFilePath ?? importSongMetadata.SourceFilePath)
                        : metadata.Title;

                    var effectiveAlbumName = metadata.Album is null || string.IsNullOrEmpty(metadata.Album.Name)
                        ? "(No Album)"
                        : metadata.Album.Name;

                    var effectiveAlbumArtistName = metadata.Album?.Artist is null || string.IsNullOrEmpty(metadata.Album!.Artist!.Name)
                        ? "(No Artist)"
                        : metadata.Album.Artist.Name;

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
                        duplicatesStrategy == DuplicateSongsHandlingStrategy.Skip)
                    {
                        logger.LogDebug("  >> SKIPPING: Duplicate checksum found. SongId={SongId}, RepositoryPath='{Path}', Strategy={Strategy}",
                            song.Id, song.RepositoryPath, duplicatesStrategy);
                        job.AddSkipReason(new DuplicateChecksumSkipReason(importSongMetadata.SourceFilePath,
                            metadata.FullLabel, checksum, checksumAlgorithmName, song.Label, song.Id));
                        job.AddSongMapping(importSongMetadata, song);
                        continue;
                    }

                    if (song is null && importSongMetadata.SongId.HasValue)
                    {
                        logger.LogDebug("  >> Loading song by SongId={SongId} from import metadata", importSongMetadata.SongId.Value);
                        song = await db.Songs
                            .Include(s => s.Devices)
                            .FirstOrDefaultAsync(s => s.Id == importSongMetadata.SongId.Value, cancellationToken);
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

                    if (metadata.Album?.CoverArt?.Biggest is not null)
                    {
                        var biggestCoverArt = metadata.Album!.CoverArt!.Biggest!;

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
                    if (song?.Album?.Name == effectiveAlbumName &&
                        song?.Album?.Artist?.Name == effectiveAlbumArtistName)
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
                        songAlbum = await repo.GetArtistAlbum(effectiveAlbumArtistName, effectiveAlbumName,
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
                        var songAlbumArtist = (await repo.GetArtists(effectiveAlbumArtistName, cancellationToken))
                            .FirstOrDefault();

                        if (songAlbumArtist is null)
                        {
                            songAlbumArtist = new Artist
                            {
                                Name = effectiveAlbumArtistName, OwnerId = userId, AlbumsCount = 1, SongsCount = 0,
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
                            Name = effectiveAlbumName, Artist = songAlbumArtist, OwnerId = userId, SongsCount = 1,
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

                            var existingSongArtist = song?.Artists?.FirstOrDefault(sa => sa.ArtistId == songArtist.Id);

                            if (existingSongArtist is null)
                            {
                                songArtist.SongsCount += 1;
                            }

                            // Add the song artist that already belonged to the song (if any) or create a new one
                            songArtists.Add(
                                existingSongArtist
                                ??
                                new SongArtist { SongId = 0, Artist = songArtist }
                            );
                        }
                    }

                    // Always ensure the album artist is in the song's artist list
                    if (!songArtists.Any(sa => sa.Artist.Name == effectiveAlbumArtistName))
                    {
                        var albumArtist = (await repo.GetArtists(effectiveAlbumArtistName, cancellationToken))
                            .FirstOrDefault();

                        if (albumArtist is null)
                        {
                            albumArtist = new Artist
                            {
                                Name = effectiveAlbumArtistName, Owner = user, AlbumsCount = 0, SongsCount = 0,
                                CreatedAt = DateTime.UtcNow,
                            };

                            await db.AddAsync(albumArtist, cancellationToken);
                        }

                        var existingAlbumSongArtist = song?.Artists?.FirstOrDefault(sa => sa.ArtistId == albumArtist.Id);

                        if (existingAlbumSongArtist is null)
                        {
                            albumArtist.SongsCount += 1;
                        }

                        songArtists.Add(
                            existingAlbumSongArtist
                            ??
                            new SongArtist { SongId = 0, Artist = albumArtist }
                        );
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
                        await targetFile.Save(sourceStream, metadata, naming,
                            async newPath => await FilePathResolver.ResolveConflictAsync(newPath, user.Id, db, cancellationToken),
                            cancellationToken);
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

                    song.Title = effectiveTitle;
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
                    song.ModifiedAt = importSongMetadata.ModifiedAt.ToUniversalTime();

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
    ///     Do not import the song if a song with the same checksum already exists in the repository.
    ///     If the file path collides with an existing song, a counter suffix is appended.
    /// </summary>
    Skip,

    /// <summary>
    ///     Replace the existing song if a song with the same checksum already exists in the repository.
    ///     If the file path collides with an existing song, a counter suffix is appended.
    /// </summary>
    Overwrite,
}
