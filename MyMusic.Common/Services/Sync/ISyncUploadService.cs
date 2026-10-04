using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public interface ISyncUploadService
{
    /// <summary>
    /// Stages an uploaded file and records what the commit does with it. When the user chose to keep the
    /// local version of a real conflict, <paramref name="resolvesConflictRecordId"/> names that
    /// <c>Conflict</c> record: the records created for the file then point to it, unless the upload failed.
    /// </summary>
    Task<SyncUploadResult> UploadAsync(
        long deviceId,
        long sessionId,
        bool isDryRun,
        string path,
        Stream fileStream,
        string fileName,
        DateTime modifiedAt,
        DateTime createdAt,
        bool isUpdate,
        SongDevice? songDeviceForImport,
        string repositoryPath,
        long ownerId,
        SyncDirection direction,
        bool deduplicate = false,
        long? resolvesConflictRecordId = null,
        CancellationToken cancellationToken = default);
}