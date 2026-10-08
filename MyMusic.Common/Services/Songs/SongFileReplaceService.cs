using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.Services.Sync;
using MyMusic.Common.Targets;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongFileReplaceService"/>.
/// </summary>
public class SongFileReplaceService(
    MusicDbContext db,
    IFileSystem fileSystem,
    IAdvisoryLockService advisoryLockService,
    IFileTransactionService fileTransactions,
    ISongFileUpdateService songFileUpdate,
    ISyncPathResolver pathResolver,
    ILogger<SongFileReplaceService> logger) : ISongFileReplaceService
{
    /// <summary>The formats the importer accepts.</summary>
    private static readonly string[] SupportedExtensions = [".mp3", ".m4a"];

    /// <inheritdoc />
    public async Task<Song> ReplaceAsync(long userId, long songId, string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        var extension = fileSystem.Path.GetExtension(sourceFilePath).ToLowerInvariant();

        if (!SupportedExtensions.Contains(extension))
        {
            throw new ValidationException(
                $"Unsupported file format '{extension}': only {string.Join(" and ", SupportedExtensions)} files are accepted");
        }

        var sourceMetadata = await ReadSourceMetadataAsync(sourceFilePath, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Imports updating this song must not interleave with the replacement
        await using var songLock = await advisoryLockService.AcquireTransactionLocksAsync(db,
            [AdvisoryLockKey.Create(AdvisoryLockScope.Song, userId, songId.ToString())], cancellationToken);

        await using var files = fileTransactions.Begin(db);

        var song = await db.Songs
            .Where(s => s.Id == songId && s.OwnerId == userId)
            .Include(s => s.Owner)
            .Include(s => s.Album)
            .ThenInclude(a => a.Artist)
            .Include(s => s.Artists)
            .ThenInclude(sa => sa.Artist)
            .Include(s => s.Genres)
            .ThenInclude(sg => sg.Genre)
            .Include(s => s.Cover)
            .Include(s => s.Devices)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (song == null)
        {
            throw new InvalidOperationException("Song not found or access denied");
        }

        if (!fileSystem.File.Exists(song.RepositoryPath))
        {
            throw new FileNotFoundException($"File of song {songId} not found at {song.RepositoryPath}");
        }

        var previousPath = song.RepositoryPath;
        var extensionChanged = !string.Equals(fileSystem.Path.GetExtension(previousPath), extension,
            StringComparison.OrdinalIgnoreCase);

        // Done on the new file while it is still outside the repository, so the song's file is only touched once
        ReplaceTags(previousPath, sourceFilePath, sameFormat: !extensionChanged);

        if (extensionChanged)
        {
            var newPath = await FilePathResolver.ResolveConflictAsync(
                fileSystem.Path.ChangeExtension(previousPath, extension), userId, songId, db, cancellationToken);

            await files.DeleteAsync(previousPath, cancellationToken);
            await files.PrepareOverwriteAsync(newPath, cancellationToken);
            fileSystem.File.Copy(sourceFilePath, newPath);

            song.RepositoryPath = newPath;
        }
        else
        {
            await files.PrepareOverwriteAsync(previousPath, cancellationToken);
            fileSystem.File.Copy(sourceFilePath, previousPath);
        }

        song.Duration = sourceMetadata.Duration;
        song.Bitrate = sourceMetadata.Bitrate;

        // Writes the song's metadata into the new file, and marks the devices to download it
        var fileUpdate = await songFileUpdate.UpdateAsync(db, files, song, () => "Song file replaced",
            cancellationToken);

        if (extensionChanged)
        {
            await ChangeDevicePathsExtensionAsync(song, extension, cancellationToken);
        }

        song.ModifiedAt = DateTime.UtcNow;
        song.Size = fileSystem.FileInfo.New(fileUpdate.PreviousPath ?? song.RepositoryPath).Length;

        await db.SaveChangesAsync(cancellationToken);

        // The file only moves once the (owner, path) unique index lets the song claim its new path
        if (fileUpdate.PreviousPath is not null)
        {
            await files.MoveAsync(fileUpdate.PreviousPath, song.RepositoryPath, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Song {SongId} file replaced: {PreviousPath} → {NewPath}, checksum {Checksum}",
            songId, previousPath, song.RepositoryPath, song.Checksum);

        return song;
    }

    /// <summary>
    /// Reads the new file the same way the importer does, which also tells whether it is a song file at all.
    /// </summary>
    private async Task<SongMetadata> ReadSourceMetadataAsync(string sourceFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await new FileTarget(fileSystem) { FilePath = sourceFilePath }.ReadMetadata(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Song file {FilePath} cannot replace a song's file", sourceFilePath);
            throw new ValidationException($"Cannot read song metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Drops every tag of the new file, and gives it the ones of the song's current file.
    /// </summary>
    private void ReplaceTags(string currentFilePath, string newFilePath, bool sameFormat)
    {
        using (var stripped = TagLib.File.Create(new FileSystemFileAbstraction(fileSystem.FileInfo.New(newFilePath))))
        {
            stripped.RemoveTags(TagLib.TagTypes.AllTags);
            stripped.Save();
        }

        using var current =
            TagLib.File.Create(new FileSystemFileAbstraction(fileSystem.FileInfo.New(currentFilePath)));
        using var replacement =
            TagLib.File.Create(new FileSystemFileAbstraction(fileSystem.FileInfo.New(newFilePath)));

        if (sameFormat)
        {
            // Tag by tag, which also carries over the frames that have no format independent property
            foreach (var tag in (current.Tag as TagLib.CombinedTag)?.Tags ?? [current.Tag])
            {
                if (tag is not null && replacement.GetTag(tag.TagTypes, true) is { } target)
                {
                    tag.CopyTo(target, true);
                }
            }
        }
        else
        {
            current.Tag.CopyTo(replacement.Tag, true);
        }

        replacement.Save();
    }

    /// <summary>
    /// Requests the song's copies on the devices to take the extension of the new file. The download they are
    /// marked for moves them there on the next sync.
    /// </summary>
    private async Task ChangeDevicePathsExtensionAsync(Song song, string extension,
        CancellationToken cancellationToken)
    {
        var copiesByDevice = song.Devices
            .Where(sd => sd.SyncAction != SongSyncAction.Remove)
            .GroupBy(sd => sd.DeviceId);

        foreach (var copies in copiesByDevice)
        {
            var usedPaths = await SongDeviceNewPaths.LoadUsedPathsAsync(db, copies.Key, cancellationToken);

            foreach (var songDevice in copies.OrderBy(sd => sd.Id))
            {
                var currentPath = songDevice.RequestedPath ?? songDevice.DevicePath;
                var basePath = Path.ChangeExtension(currentPath, extension);

                if (basePath == currentPath)
                {
                    continue;
                }

                if (songDevice.RequestedPath != null && songDevice.RequestedPath != songDevice.DevicePath)
                {
                    usedPaths.Remove(songDevice.RequestedPath);
                }

                // A file the device already holds keeps its path (where the device reports it) until the next sync
                var neverDownloaded = songDevice is { SyncAction: SongSyncAction.Download, LastSyncedModifiedAt: null };
                if (neverDownloaded)
                {
                    usedPaths.Remove(songDevice.DevicePath);
                }

                var path = pathResolver.GetUniquePath(basePath, usedPaths);
                usedPaths.Add(path);

                songDevice.RequestedPath = path;
                if (neverDownloaded)
                {
                    songDevice.DevicePath = path;
                }
            }
        }
    }
}
