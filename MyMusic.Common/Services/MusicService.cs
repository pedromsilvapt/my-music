using DotNext.Threading;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Models;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Targets;
using System.IO.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using Microsoft.Extensions.Options;

namespace MyMusic.Common.Services;

public class MusicService(IFileSystem fileSystem, IOptions<Config> config, ILogger logger)
    : IMusicService
{
    public const string MusicIgnoreFile = ".musicignore";
    public ILogger Logger { get; set; } = logger;

    public IFileSystem FileSystem { get; set; } = fileSystem;
    public Config Config { get; set; } = config.Value;

    /// <summary>
    /// Repository management actions involve creating, updating, or removing songs from the repository, as well as adding, updating or removing existing songs from a repository.
    /// To prevent concurrent edits (which may result in duplicated songs or strange things like that) we have this lock acting as a mutex.
    /// Ideally we could only lock operations for the specific repository we are working with, but for now, we will not delve into that extra complexity.
    /// </summary>
    private readonly AsyncReaderWriterLock _repositoryManagementLock = new AsyncReaderWriterLock();

    #region Device

    /// <summary>
    /// Creates a new music device, associated with the given repositoryId.
    /// </summary>
    /// <param name="db"></param>
    /// <param name="name"></param>
    /// <param name="ownerId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Device> CreateDevice(MusicDbContext db, string name, long ownerId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([ownerId], cancellationToken: cancellationToken);

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
    /// Adds a music to a device (if it is not added already)
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
        var songDevice = await db.SongDevices.FirstOrDefaultAsync(sd => sd.DeviceId == deviceId && sd.SongId == song.Id,
            cancellationToken);

        var namingStrategy = new ArtistAlbumNamingStrategy();

        if (songDevice == null)
        {
            songDevice = new SongDevice
            {
                DeviceId = deviceId,
                SongId = song.Id,
                SyncAction = SongSyncAction.Download,
                DevicePath = namingStrategy.Generate(EntityConverter.ToSong(song)),
                AddedAt = DateTime.Now,
            };

            await db.AddAsync(songDevice, cancellationToken);
        }
        else if (songDevice.SyncAction == SongSyncAction.Remove)
        {
            songDevice.SyncAction = null;
            songDevice.DevicePath = namingStrategy.Generate(EntityConverter.ToSong(song));

            db.Update(songDevice);
        }
    }

    /// <summary>
    /// Removes a music from a device (if it is added already)
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
    /// Returns a dictionary with the list of songs matching the checksums provided in the list.
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
    /// 
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
            if (FileSystem.File.Exists(FileSystem.Path.Combine(sourceFolder, MusicIgnoreFile)))
            {
                continue;
            }

            var files = FileSystem.Directory.GetFiles(sourceFolder);

            foreach (var filePath in files)
            {
                var extension = FileSystem.Path.GetExtension(filePath);

                if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".m4a", StringComparison.OrdinalIgnoreCase))
                {
                    importedSongs.Add(new SongImportMetadata(filePath, FileSystem.File.GetCreationTimeUtc(filePath),
                        FileSystem.File.GetLastWriteTimeUtc(filePath)));
                }
            }

            // If this function was called to scan all subdirectories as well
            if (searchOption == SearchOption.AllDirectories)
            {
                var subFolders = FileSystem.Directory.GetDirectories(sourceFolder);

                foreach (var subFolder in subFolders)
                {
                    sourceFoldersQueue.Push(subFolder);
                }
            }
        }

        await ImportRepositorySongs(db, job, userId, importedSongs, deviceIds, duplicatesStrategy, cancellationToken);
    }

    /// <summary>
    /// 
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
        Logger.LogDebug("Acquiring lock to begin importing songs into repository Id {RepositoryId}", userId);

        using (await _repositoryManagementLock.AcquireWriteLockAsync(cancellationToken))
        {
            Logger.LogDebug("  >> Lock acquired");

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
            var checksumAlgorithm = CreateChecksumAlgorithm();
            var checksumAlgorithmName = checksumAlgorithm.GetType().Name;

            foreach (var importSongMetadata in importSongsMetadataList)
            {
                await using var dbTrans = await db.Database.BeginTransactionAsync(cancellationToken);

                Logger.LogDebug("Importing song from file {SongFilePath}", importSongMetadata.SourceFilePath);

                SongMetadata? metadata = null;

                try
                {
                    var sourceFile = new FileTarget(FileSystem) { FilePath = importSongMetadata.SourceFilePath };
                    var targetFile = new FileTarget(FileSystem)
                        { Folder = FileSystem.Path.Join(Config.MusicRepositoryPath, user.Username) };

                    metadata = await sourceFile.ReadMetadata(cancellationToken);

                    // Get the length of the file
                    var duration = metadata.Duration;

                    Logger.LogDebug("  >> Metadata read: {Song}", metadata.FullLabel);

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

                    var checksum = CalculateChecksum(FileSystem, checksumAlgorithm, sourceFile.FilePath);

                    Logger.LogDebug("  >> Checksum calculated: {Checksum}", checksum);

                    var song = await repo.GetSongByChecksum(checksum, checksumAlgorithmName, cancellationToken);

                    // If there is a song with the same checksum, it means all fields are the same. And since Naming Strategies should be pure functions,
                    // we can safely assume that the file name will also be the same as well.
                    if (song is not null &&
                        File.Exists(song.RepositoryPath) &&
                        (duplicatesStrategy == DuplicateSongsHandlingStrategy.Skip ||
                         duplicatesStrategy == DuplicateSongsHandlingStrategy.SkipIdentical))
                    {
                        job.AddSkipReason(new DuplicateChecksumSkipReason(importSongMetadata.SourceFilePath,
                            metadata.FullLabel, checksum, checksumAlgorithmName, song.Label, song.Id));
                        continue;
                    }

                    // Pre-calculate the path that will be used by the configured naming strategy
                    targetFile.EnsureFilePath(metadata);

                    // If no match with the same checksum was found, try to find with the same target file path
                    song ??= await repo.GetSongByPath(targetFile.FilePath!, cancellationToken);

                    if (song is not null &&
                        File.Exists(song.RepositoryPath) &&
                        duplicatesStrategy == DuplicateSongsHandlingStrategy.Skip)
                    {
                        job.AddSkipReason(new DuplicateFilePathSkipReason(importSongMetadata.SourceFilePath,
                            metadata.FullLabel, targetFile.FilePath!, song.Label, song.Id));
                        continue;
                    }

                    if (song is not null)
                    {
                        // TODO Load devices
                        // await db.LoadReferencesAsync<Song, SongArtist>(song, token: cancellationToken);
                        // await db.LoadReferencesAsync<Song, SongGenre>(song, token: cancellationToken);
                        // await db.LoadReferencesAsync<Song, Album>(song, token: cancellationToken);
                        // await db.LoadReferencesAsync<Song, Album, Artist>(song, token: cancellationToken);
                        // await db.LoadReferencesAsync<Song, Artwork>(song, token: cancellationToken);
                        // var entry = db.Entry(song);
                        // await entry.Collection(s => s.Artists).LoadAsync(cancellationToken);
                        // await entry.Collection(s => s.Genres).LoadAsync(cancellationToken);
                        // await entry.Reference(s => s.Album).LoadAsync(cancellationToken);
                        // await entry.Reference(s => s.Album.Artist).LoadAsync(cancellationToken);
                        // await entry.Reference(s => s.Cover).LoadAsync(cancellationToken);

                        song = await db.Songs
                            .Include(s => s.Artists)
                            .Include(s => s.Genres)
                            .Include(s => s.Cover)
                            .Include(s => s.Album)
                            .ThenInclude(a => a!.Artist)
                            .FirstAsync(s => s.Id == song.Id, cancellationToken);
                    }

                    ImageBuffer? cover = null;

                    if (metadata.Album.CoverArt?.Biggest is not null)
                    {
                        var biggestCoverArt = metadata.Album.CoverArt.Biggest!;

                        if (biggestCoverArt.Length > 500)
                        {
                            Logger.LogDebug("Downloading cover {CoverUrl}", biggestCoverArt.Substring(0, 1000));
                        }
                        else
                        {
                            Logger.LogDebug("Downloading cover {CoverUrl}", biggestCoverArt);
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
                                Name = metadata.Album.Artist.Name, OwnerId = userId, AlbumsCount = 1, SongsCount = 0
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
                            Name = metadata.Album.Name, Artist = songAlbumArtist, OwnerId = userId, SongsCount = 1
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
                                    { Name = artist.Name, Owner = user, AlbumsCount = 0, SongsCount = 0 };

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

                    var size = FileSystem.FileInfo.New(sourceFile.FilePath).Length;

                    await using (var sourceStream = sourceFile.Read())
                    {
                        await targetFile.Save(sourceStream, metadata, cancellationToken: cancellationToken);
                    }

                    job.AddFileMapping(sourceFile.FilePath!, targetFile.FilePath!);

                    await targetFile.SetTimestamps(importSongMetadata.CreatedAt, importSongMetadata.ModifiedAt,
                        cancellationToken);

                    if (song is null)
                    {
                        // Use Activator to avoid having to specify all the required fields manually
                        song = Activator.CreateInstance<Song>();

                        // Timestamps
                        song.CreatedAt = importSongMetadata.CreatedAt;
                        song.ModifiedAt = importSongMetadata.ModifiedAt;
                        song.AddedAt = DateTime.Now;
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
                    // Checksum
                    song.Checksum = checksum;
                    song.ChecksumAlgorithm = checksumAlgorithmName;
                    // References
                    song.Album = songAlbum;
                    song.Genres = songGenres;
                    song.Devices = songDevices;
                    song.Artists = songArtists;
                    // Timestamps
                    song.ModifiedAt = importSongMetadata.CreatedAt;

                    //if (existingSong is not null && duplicatesStrategy == DuplicateSongsHandlingStrategy.Overwrite)
                    //{
                    //    // TODO Need to check, if when updating an entity, old relations are deleted or if they are kept.
                    //    song.Id = existingSong.Id;
                    //    song.AddedAt = existingSong.AddedAt;
                    //    song.CreatedAt = existingSong.CreatedAt < song.CreatedAt ? existingSong.CreatedAt : song.CreatedAt;
                    //    song.ModifiedAt = existingSong.ModifiedAt > song.ModifiedAt ? existingSong.ModifiedAt : song.ModifiedAt;
                    //}

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
                }
                catch (TaskCanceledException)
                {
                    await dbTrans.RollbackAsync(cancellationToken);

                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to import song {SongLabel} from file {File}",
                        metadata?.FullLabel ?? "(undefined)", importSongMetadata.SourceFilePath);

                    await dbTrans.RollbackAsync(cancellationToken);
                }
            }
        }

        Logger.LogDebug("  >> Lock released");
    }

    public static NonCryptographicHashAlgorithm CreateChecksumAlgorithm()
    {
        return new XxHash128();
    }

    public static string CalculateChecksum(IFileSystem fs, NonCryptographicHashAlgorithm algorithm, string filePath)
    {
        using var file = fs.File.OpenRead(filePath);
        
        return CalculateChecksum(algorithm, file);
    }

    public static string CalculateChecksum(NonCryptographicHashAlgorithm algorithm, byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);
        
        return CalculateChecksum(algorithm, memory);
    }

    public static string CalculateChecksum(NonCryptographicHashAlgorithm algorithm, Stream stream)
    {
        algorithm.Append(stream);

        var hash = algorithm.GetHashAndReset();

        return Convert.ToBase64String(hash);
    }

    #endregion
}

public enum DuplicateSongsHandlingStrategy
{
    /// <summary>
    /// Do not import the song if it already exists in the repository (same checksum or same file path)
    /// </summary>
    Skip,

    /// <summary>
    /// Do not import the song if it already exists in the repository as an exact match (same checksum)
    /// </summary>
    SkipIdentical,

    /// <summary>
    /// Replace the existing song, if it already exists in the repository (same checksum or same file path)
    /// </summary>
    Overwrite,
}