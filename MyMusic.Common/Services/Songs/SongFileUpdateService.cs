using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;
using MyMusic.Common.Metadata;
using MyMusic.Common.NamingStrategies;
using MyMusic.Common.Targets;
using MyMusic.Common.Utilities;

namespace MyMusic.Common.Services.Songs;

/// <summary>
/// Default implementation of <see cref="ISongFileUpdateService"/>.
/// </summary>
public class SongFileUpdateService(
    IFileSystem fileSystem,
    IOptions<Config> config) : ISongFileUpdateService
{
    /// <inheritdoc />
    public async Task<SongFileUpdateResult> UpdateAsync(MusicDbContext db, IFileTransaction files, Song song,
        Func<string> downloadReason, CancellationToken cancellationToken = default)
    {
        var previousChecksum = song.Checksum;
        var metadata = EntityConverter.ToSong(song);

        var fileTarget = new FileTarget(fileSystem)
        {
            FilePath = song.RepositoryPath,
            Folder = fileSystem.Path.Join(config.Value.MusicRepositoryPath, song.Owner.Username),
        };

        // The file is edited in place, and restored if the transaction fails
        await files.PrepareEditAsync(song.RepositoryPath, cancellationToken);
        await fileTarget.SaveMetadata(metadata, cancellationToken);

        var naming = NamingMetadata.FromPath(song.RepositoryPath);
        var newPath = await fileTarget.GetRelocatedPath(naming, async path =>
                await FilePathResolver.ResolveConflictAsync(path, song.OwnerId, song.Id, db, cancellationToken),
            cancellationToken);

        // The file is still at its current path
        var checksumAlgorithm = ChecksumService.CreateChecksumAlgorithm();
        song.Checksum = ChecksumService.CalculateChecksum(fileSystem, checksumAlgorithm, song.RepositoryPath);
        song.ChecksumAlgorithm = checksumAlgorithm.GetType().Name;

        var previousPath = newPath != song.RepositoryPath ? song.RepositoryPath : null;

        song.RepositoryPath = newPath;

        song.Label = SongLabelBuilder.Build(song);

        var checksumChanged = song.Checksum != previousChecksum;
        if (checksumChanged)
        {
            await MarkSongDevicesForDownloadAsync(db, song.Id, downloadReason(), cancellationToken);

            // The file content actually changed: record the file-level modification time
            song.FileModifiedAt = DateTime.UtcNow;
        }

        return new SongFileUpdateResult { PreviousPath = previousPath, ChecksumChanged = checksumChanged };
    }

    private static async Task MarkSongDevicesForDownloadAsync(MusicDbContext db, long songId,
        string reason, CancellationToken cancellationToken)
    {
        var songDevices = await db.SongDevices
            .Where(sd => sd.SongId == songId && sd.SyncAction != SongSyncAction.Remove)
            .ToListAsync(cancellationToken);

        foreach (var songDevice in songDevices)
        {
            songDevice.SyncAction = SongSyncAction.Download;
            songDevice.SyncActionReason = reason;
        }
    }
}
