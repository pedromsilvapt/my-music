using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public interface ISyncUploadService
{
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
        CancellationToken cancellationToken = default);
}