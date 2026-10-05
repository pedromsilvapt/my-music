using System.IO.Abstractions;
using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Targets;
using MyMusic.Common.Utilities;
using Npgsql;

namespace MyMusic.Common.Services;

public class MusicService(
    IFileSystem fileSystem,
    IOptions<Config> config,
    IDbContextFactory<MusicDbContext> dbContextFactory,
    ISongMergeService songMergeService,
    IAdvisoryLockService advisoryLockService,
    IUserImportThrottle importThrottle,
    IFileTransactionService fileTransactions,
    ILogger<MusicService> logger)
    : IMusicService
{
    public const string MusicIgnoreFile = ".musicignore";

    /// <summary>
    ///     How many times a song import is attempted when it fails because of a concurrent write.
    /// </summary>
    private const int MaxImportAttempts = 3;

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
    ///     Returns a dictionary with the list of songs matching the checksums provided in the list, either by their
    ///     current file or by a previous version of it (see <see cref="UserMusicService.FindSongsByChecksums"/>).
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
        // No lock needed: only committed songs are visible, and imports hold the song's checksum lock until they commit
        return await new UserMusicService(db, userId).FindSongsByChecksums(checksums, checksumAlgorithm, cancellationToken);
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
            // Throttled per song rather than per batch, so a long folder import cannot starve the user's other imports
            using var importSlot = await importThrottle.AcquireAsync(userId, cancellationToken);

            await ImportSong(job, user, importSongMetadata, checksumAlgorithm, checksumAlgorithmName,
                duplicatesStrategy, cancellationToken);
        }
    }

    /// <summary>
    ///     Imports a single song. Failures are recorded on the <paramref name="job"/> instead of thrown, except for
    ///     cancellation.
    /// </summary>
    private async Task ImportSong(MusicImportJob job, User user,
        SongImportMetadata importSongMetadata, NonCryptographicHashAlgorithm checksumAlgorithm,
        string checksumAlgorithmName, DuplicateSongsHandlingStrategy duplicatesStrategy,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Importing song from file {SongFilePath}", importSongMetadata.SourceFilePath);

        SongMetadata? metadata = null;
        PreparedSongImport prepared;

        try
        {
            var sourceFile = new FileTarget(fileSystem) { FilePath = importSongMetadata.SourceFilePath };

            metadata = await sourceFile.ReadMetadata(cancellationToken);

            logger.LogDebug("  >> Metadata read: {Song}", metadata.FullLabel);

            prepared = await PrepareSongImport(user, importSongMetadata, sourceFile, metadata, checksumAlgorithm,
                checksumAlgorithmName, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordImportFailure(job, importSongMetadata, metadata, ex);
            return;
        }

        for (var attemptNumber = 1; ; attemptNumber++)
        {
            var canRetry = attemptNumber < MaxImportAttempts;

            if (await TryImportSongAttempt(job, user, importSongMetadata, prepared, duplicatesStrategy, canRetry,
                    cancellationToken))
            {
                return;
            }

            logger.LogWarning(
                "Retrying import of song {SongLabel} from file {File} after a concurrency conflict (attempt {Attempt} of {MaxAttempts})",
                prepared.Metadata.FullLabel, importSongMetadata.SourceFilePath, attemptNumber + 1, MaxImportAttempts);
        }
    }

    /// <summary>
    ///     Reads and computes everything the import needs from the source file. Runs before the song's transaction, so
    ///     no locks are held while hashing the file or decoding its cover.
    /// </summary>
    private async Task<PreparedSongImport> PrepareSongImport(User user, SongImportMetadata importSongMetadata,
        FileTarget sourceFile, SongMetadata metadata, NonCryptographicHashAlgorithm checksumAlgorithm,
        string checksumAlgorithmName, CancellationToken cancellationToken)
    {
        var naming = NamingMetadata.FromPath(importSongMetadata.OriginalFilePath ?? sourceFile.FilePath);

        #region Placeholder Values for Missing Metadata

        // Use placeholder values for missing metadata fields when writing to the database.
        // Do NOT mutate the metadata object — it is used for file operations (path generation,
        // tag writing) and must reflect the actual file contents to preserve checksum integrity.

        var effectiveTitle = string.IsNullOrEmpty(metadata.Title)
            ? fileSystem.Path.GetFileNameWithoutExtension(importSongMetadata.OriginalFilePath ?? importSongMetadata.SourceFilePath)
            : metadata.Title;

        var effectiveAlbumName = metadata.Album is null || string.IsNullOrEmpty(metadata.Album.Name)
            ? Album.PlaceholderName
            : metadata.Album.Name;

        var effectiveAlbumArtistName = metadata.Album?.Artist is null || string.IsNullOrEmpty(metadata.Album!.Artist!.Name)
            ? Artist.PlaceholderName
            : metadata.Album.Artist.Name;

        #endregion

        var checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, sourceFile.FilePath!);

        logger.LogDebug("  >> Checksum calculated: {Checksum} ({Algorithm}) for file {FilePath}",
            checksum, checksumAlgorithmName, importSongMetadata.SourceFilePath);

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

        #region Lock Keys

        // Everything this song may find-or-create, or collide with, that no unique index protects. Concurrent imports
        // of songs sharing any of these keys run one after the other; all other imports run in parallel.
        // Genres need no lock: their (owner, name) unique index makes them safe to upsert. Neither do repository
        // paths: their (owner, path) unique index makes a concurrent claim of the same path fail, and be retried.

        var lockKeys = new List<AdvisoryLockKey>
        {
            // Two uploads of the same file must not both pass the duplicate check
            AdvisoryLockKey.Create(AdvisoryLockScope.SongChecksum, user.Id, checksumAlgorithmName, checksum),
            AdvisoryLockKey.Create(AdvisoryLockScope.Album, user.Id, effectiveAlbumArtistName, effectiveAlbumName),
        };

        var artistNames = (metadata.Artists?.Select(artist => artist.Name) ?? []).Append(effectiveAlbumArtistName);

        foreach (var artistName in artistNames.Distinct())
        {
            lockKeys.Add(AdvisoryLockKey.Create(AdvisoryLockScope.Artist, user.Id, artistName));
        }

        if (importSongMetadata.SongId is { } songId)
        {
            // Updates (and merges) of the same existing song must not interleave
            lockKeys.Add(AdvisoryLockKey.Create(AdvisoryLockScope.Song, user.Id, songId.ToString()));
        }

        #endregion Lock Keys

        return new PreparedSongImport(sourceFile, naming, metadata, effectiveTitle, effectiveAlbumName,
            effectiveAlbumArtistName, checksum, checksumAlgorithmName, cover, lockKeys);
    }

    /// <summary>
    ///     Runs one attempt at persisting the song in its own context and transaction, while holding the song's
    ///     advisory locks.
    /// </summary>
    /// <returns>
    ///     <c>false</c> when the attempt was rolled back because of a transient concurrency failure and should be
    ///     retried; <c>true</c> otherwise (including failures, which are recorded on the <paramref name="job"/>).
    /// </returns>
    private async Task<bool> TryImportSongAttempt(MusicImportJob job, User user,
        SongImportMetadata importSongMetadata, PreparedSongImport prepared,
        DuplicateSongsHandlingStrategy duplicatesStrategy, bool canRetry, CancellationToken cancellationToken)
    {
        var attempt = new SongImportAttempt();

        IAsyncDisposable? songLocks = null;

        try
        {
            // A context of its own: discarding it, along with the rolled back transaction, undoes everything a failed
            // attempt did, so nothing of it can leak into a retry, the next song, or the caller's context
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var dbTrans = await db.Database.BeginTransactionAsync(cancellationToken);

            // Rolled back along with the transaction, which undoes the file changes of a failed attempt
            await using var files = fileTransactions.Begin(db);

            try
            {
                logger.LogDebug("  >> Acquiring {LockCount} import locks", prepared.LockKeys.Count);

                songLocks = await advisoryLockService.AcquireTransactionLocksAsync(db, prepared.LockKeys,
                    cancellationToken);

                logger.LogDebug("  >> Import locks acquired");

                await PersistSong(db, job, user, importSongMetadata, prepared, duplicatesStrategy, dbTrans, files,
                    attempt, cancellationToken);

                return true;
            }
            catch (TaskCanceledException)
            {
                await RollbackAttempt(dbTrans, attempt, cancellationToken);

                throw;
            }
            catch (Exception ex) when (canRetry && !attempt.Committed && IsTransientConcurrencyFailure(ex))
            {
                logger.LogWarning(ex, "Concurrency conflict while importing song {SongLabel} from file {File}",
                    prepared.Metadata.FullLabel, importSongMetadata.SourceFilePath);

                await RollbackAttempt(dbTrans, attempt, cancellationToken);

                return false;
            }
            catch (Exception ex)
            {
                RecordImportFailure(job, importSongMetadata, prepared.Metadata, ex);

                await RollbackAttempt(dbTrans, attempt, cancellationToken);

                return true;
            }
        }
        finally
        {
            // Released only once the transaction is over (for in-process implementations; the database releases its
            // own advisory locks on commit/rollback), so nobody can see this song half-imported
            if (songLocks is not null)
            {
                await songLocks.DisposeAsync();
            }
        }
    }

    private async Task PersistSong(MusicDbContext db, MusicImportJob job, User user,
        SongImportMetadata importSongMetadata, PreparedSongImport prepared,
        DuplicateSongsHandlingStrategy duplicatesStrategy, IDbContextTransaction dbTrans, IFileTransaction files,
        SongImportAttempt attempt, CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var repo = new UserMusicService(db, userId);

        var (sourceFile, naming, metadata, effectiveTitle, effectiveAlbumName, effectiveAlbumArtistName, checksum,
            checksumAlgorithmName, cover, _) = prepared;

        var duration = metadata.Duration;

        var targetFile = CreateTargetFile(user);

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

        // The file is an older version of an existing song: the duplicates strategy decides whether it is skipped,
        // overwrites the song's newer content, or becomes a song of its own
        var isPreviousVersion = song is not null &&
                                (song.Checksum != checksum || song.ChecksumAlgorithm != checksumAlgorithmName);

        if (song is not null && isPreviousVersion)
        {
            switch (duplicatesStrategy)
            {
                case DuplicateSongsHandlingStrategy.Skip:
                    logger.LogDebug("  >> SKIPPING: Checksum matches a previous version of SongId={SongId} (current checksum {CurrentChecksum})",
                        song.Id, song.Checksum);
                    job.AddSkipReason(new PreviousVersionChecksumSkipReason(importSongMetadata.SourceFilePath,
                        metadata.FullLabel, checksum, checksumAlgorithmName, song.Label, song.Id));
                    job.AddSongMapping(importSongMetadata, song);
                    return;

                case DuplicateSongsHandlingStrategy.SplitWhenSuperseded:
                    // Imported as a song of its own (or into the song it was imported for), leaving the existing
                    // song untouched; the checksum then leaves its history (see the AddSongChecksumHistory trigger)
                    logger.LogDebug("  >> SPLITTING: Checksum matches a previous version of SongId={SongId}, importing it as a separate song",
                        song.Id);
                    song = null;
                    break;

                case DuplicateSongsHandlingStrategy.Overwrite:
                    // Updated below with this file: its checksum becomes current again, and the replaced one
                    // becomes a previous version (see the AddSongChecksumHistory trigger)
                    logger.LogDebug("  >> OVERWRITING: Checksum matches a previous version of SongId={SongId}, restoring it",
                        song.Id);
                    break;
            }
        }

        if (song is not null &&
            !isPreviousVersion &&
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
                return;
            }

            logger.LogDebug("  >> MERGE SUCCEEDED: Merged song {MergeFromSongId} into {KeepSongId}", importSongMetadata.SongId.Value, song.Id);

            await dbTrans.CommitAsync(cancellationToken);
            attempt.Committed = true;

            job.AddSongMapping(importSongMetadata, song);
            return;
        }

        if (song is not null &&
            !isPreviousVersion &&
            fileSystem.File.Exists(song.RepositoryPath) &&
            duplicatesStrategy is DuplicateSongsHandlingStrategy.Skip or DuplicateSongsHandlingStrategy.SplitWhenSuperseded)
        {
            logger.LogDebug("  >> SKIPPING: Duplicate checksum found. SongId={SongId}, RepositoryPath='{Path}', Strategy={Strategy}",
                song.Id, song.RepositoryPath, duplicatesStrategy);
            job.AddSkipReason(new DuplicateChecksumSkipReason(importSongMetadata.SourceFilePath,
                metadata.FullLabel, checksum, checksumAlgorithmName, song.Label, song.Id));
            job.AddSongMapping(importSongMetadata, song);
            return;
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

        Album? songAlbum = null;
        var songGenres = new List<SongGenre>();
        var songDevices = new List<SongDevice>();
        var songArtists = new List<SongArtist>();

        #region Album

        // Album and artist matching is a deliberate best-effort heuristic based on names only.
        // Two different real-world artists can share the same name, so artist names cannot be
        // unique (and there is no unique index on them). Given only the file tags, the best we
        // can do is:
        //   1. If the user already has an album with this name, by an artist with this name,
        //      that album is most likely the correct match.
        //   2. Otherwise, if the user already has an artist with this name, that artist is most
        //      likely the correct match, and a new album is created under it.
        //   3. Otherwise, a new artist (and album) is created.
        // Other scenarios (e.g. a genuinely different artist that happens to share a name with an
        // existing one) are rare. They will usually only affect the first imported song of that
        // artist/album, and are expected to be corrected manually by the user (by design).
        // Once corrected, future imports for that album/artist will match the corrected entities.

        // We are updating an existing song, and the Album remains the same
        if (song?.Album?.Name == effectiveAlbumName &&
            song?.Album?.Artist?.Name == effectiveAlbumArtistName)
        {
            songAlbum = song.Album;
        }

        // If we are updating an existing song, but the album/album artist changed, or
        // if we are creating a new song
        if (songAlbum is null)
        {
            // Rule 1: an existing album with the same name, by an artist with the same name,
            // is most likely the same album (see the heuristic described above)
            songAlbum = await repo.GetArtistAlbum(effectiveAlbumArtistName, effectiveAlbumName,
                cancellationToken);
        }

        // If no Album with this name belonging to this Artist exists on the database yet
        if (songAlbum is null)
        {
            // Rule 2: an existing artist with the same name is most likely the same artist.
            // If several exist, we cannot tell them apart, so any of them is an equally good guess.
            // Rule 3: otherwise, this is a new artist
            var songAlbumArtist = (await repo.GetArtists(effectiveAlbumArtistName, cancellationToken))
                .FirstOrDefault();

            if (songAlbumArtist is null)
            {
                songAlbumArtist = new Artist
                {
                    Name = effectiveAlbumArtistName,
                    OwnerId = userId,
                    CreatedAt = DateTime.UtcNow,
                };

                await db.AddAsync(songAlbumArtist, cancellationToken);
            }

            songAlbum = new Album
            {
                Name = effectiveAlbumName,
                Artist = songAlbumArtist,
                OwnerId = userId,
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
                // Same heuristic as for the album artist (see the Album region above): an existing
                // artist with the same name is most likely the same artist, otherwise create a new one
                var songArtist = (await repo.GetArtists(artist.Name, cancellationToken)).FirstOrDefault();

                if (songArtist is null)
                {
                    songArtist = new Artist
                    {
                        Name = artist.Name,
                        OwnerId = userId,
                        CreatedAt = DateTime.UtcNow,
                    };

                    await db.AddAsync(songArtist, cancellationToken);
                }

                var existingSongArtist = song?.Artists?.FirstOrDefault(sa => sa.ArtistId == songArtist.Id);

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
            // Same name-based artist matching as above (see the Album region)
            var albumArtist = (await repo.GetArtists(effectiveAlbumArtistName, cancellationToken))
                .FirstOrDefault();

            if (albumArtist is null)
            {
                albumArtist = new Artist
                {
                    Name = effectiveAlbumArtistName,
                    OwnerId = userId,
                    CreatedAt = DateTime.UtcNow,
                };

                await db.AddAsync(albumArtist, cancellationToken);
            }

            var existingAlbumSongArtist = song?.Artists?.FirstOrDefault(sa => sa.ArtistId == albumArtist.Id);

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
            var genreNames = metadata.Genres.Distinct().ToList();

            // Upserted in a stable order: an upsert waits on another transaction's uncommitted insert of
            // the same genre, so two songs listing the same new genres in different orders could deadlock
            var genresByName = new Dictionary<string, Genre>();

            foreach (var genreName in genreNames.Order(StringComparer.Ordinal))
            {
                genresByName[genreName] = await repo.UpsertGenre(genreName, cancellationToken);
            }

            foreach (var genreName in genreNames)
            {
                var songGenre = genresByName[genreName];

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

        // An existing song's own path is not a conflict: it keeps it, unless its metadata now generates another one
        var existingSongId = song?.Id;
        var previousRepositoryPath = song?.RepositoryPath;

        await targetFile.EnsureFilePath(metadata, naming,
            async newPath => await FilePathResolver.ResolveConflictAsync(newPath, userId, existingSongId, db,
                cancellationToken));

        if (song is null)
        {
            // Use Activator to avoid having to specify all the required fields manually
            song = Activator.CreateInstance<Song>();

            // Timestamps
            song.CreatedAt = importSongMetadata.CreatedAt.ToUniversalTime();
            song.ModifiedAt = importSongMetadata.ModifiedAt.ToUniversalTime();
            song.FileModifiedAt = importSongMetadata.ModifiedAt.ToUniversalTime();
            song.AddedAt = DateTime.UtcNow;
            // Repository Id
            song.OwnerId = userId;

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
            db.RemoveRange(artistsDiff.Removed);
        }

        if (genresDiff.Removed.Count != 0)
        {
            db.RemoveRange(genresDiff.Removed);
        }

        // Capture the previous checksum before overwriting it below; used to decide
        // whether the file content actually changed (and thus whether the file-level
        // modification timestamps should be bumped).
        var previousChecksum = song.Checksum;

        song.RepositoryPath = targetFile.FilePath!;
        song.Title = effectiveTitle;
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
        // Built from the song (not the file's tags) so it reflects the placeholder values used for missing metadata
        song.Label = SongLabelBuilder.Build(song);

        // ModifiedAt reflects any row-level change and must bump on every re-import,
        // since not all DB-row fields impact the file (e.g. rating, lyrics, labels).
        song.ModifiedAt = importSongMetadata.ModifiedAt.ToUniversalTime();

        // FileModifiedAt tracks the file-content change time and only bumps when the
        // checksum actually changes, so metadata-only re-imports don't trigger
        // unnecessary device updates.
        if (checksum != previousChecksum)
        {
            song.FileModifiedAt = importSongMetadata.ModifiedAt.ToUniversalTime();
        }

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

        // The path is claimed in the database before the file is written: a concurrent song claiming the same path
        // fails here on the unique index (and is retried), instead of both writing the same file
        await db.SaveChangesAsync(cancellationToken);

        if (previousRepositoryPath is not null && previousRepositoryPath != targetFile.FilePath &&
            fileSystem.File.Exists(previousRepositoryPath))
        {
            // The song moves to another path. Its old file is set aside, to be restored if the attempt fails
            await files.DeleteAsync(previousRepositoryPath, cancellationToken);
        }

        // The new file is written from scratch, while any file already at its path is set aside
        await files.PrepareOverwriteAsync(targetFile.FilePath!, cancellationToken);

        await using (var sourceStream = sourceFile.Read())
        {
            await targetFile.Save(sourceStream, metadata, naming, cancellationToken: cancellationToken);
        }

        await targetFile.SetTimestamps(importSongMetadata.CreatedAt, importSongMetadata.ModifiedAt,
            cancellationToken);

        await dbTrans.CommitAsync(cancellationToken);
        attempt.Committed = true;

        // Recorded only once committed: a failed attempt deletes its file, and a retry may pick another path
        job.AddFileMapping(sourceFile.FilePath!, targetFile.FilePath!);
        job.AddSongMapping(importSongMetadata, song);
    }

    private FileTarget CreateTargetFile(User user) =>
        new(fileSystem) { Folder = fileSystem.Path.Join(config.Value.MusicRepositoryPath, user.Username) };

    /// <summary>
    ///     Undoes a failed attempt: rolls back its transaction, and with it the attempt's file changes. What the
    ///     attempt's context still tracks is discarded along with the context.
    /// </summary>
    private async Task RollbackAttempt(IDbContextTransaction dbTrans, SongImportAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (attempt.Committed)
        {
            return;
        }

        await dbTrans.RollbackAsync(cancellationToken);
    }

    private void RecordImportFailure(MusicImportJob job, SongImportMetadata importSongMetadata,
        SongMetadata? metadata, Exception ex)
    {
        job.AddException(new Exception(
            $"Failed to import song {metadata?.FullLabel ?? "(undefined)"} from file {importSongMetadata.SourceFilePath}",
            ex));

        logger.LogError(ex, "Failed to import song {SongLabel} from file {File}",
            metadata?.FullLabel ?? "(undefined)", importSongMetadata.SourceFilePath);
    }

    /// <summary>
    ///     Deadlocks and serialization failures can still happen against writers that do not take the import locks
    ///     (e.g. song edits touching the same album/artist counters), and so can unique violations when such a writer
    ///     creates the same album or genre. Retrying the song resolves all of them.
    /// </summary>
    private static bool IsTransientConcurrencyFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure
                    or PostgresErrorCodes.UniqueViolation,
                })
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PreparedSongImport(
        FileTarget SourceFile,
        NamingMetadata Naming,
        SongMetadata Metadata,
        string EffectiveTitle,
        string EffectiveAlbumName,
        string EffectiveAlbumArtistName,
        string Checksum,
        string ChecksumAlgorithmName,
        ImageBuffer? Cover,
        IReadOnlyList<AdvisoryLockKey> LockKeys);

    private sealed class SongImportAttempt
    {
        public bool Committed { get; set; }
    }

    #endregion
}

public enum DuplicateSongsHandlingStrategy
{
    /// <summary>
    ///     Do not import the song if a song with the same checksum already exists in the repository, or if the
    ///     file matches a previous version of a song. If the file path collides with an existing song, a counter
    ///     suffix is appended.
    /// </summary>
    Skip,

    /// <summary>
    ///     Replace the existing song if a song with the same checksum already exists in the repository. A file
    ///     matching a previous version of a song replaces that song's file too, making that version current again.
    ///     If the file path collides with an existing song, a counter suffix is appended.
    /// </summary>
    Overwrite,

    /// <summary>
    ///     Like <see cref="Skip"/> when the file matches a song's current checksum. When it matches only a previous
    ///     version of a song, it is imported as a separate song, and the existing song is left untouched.
    /// </summary>
    SplitWhenSuperseded,
}
