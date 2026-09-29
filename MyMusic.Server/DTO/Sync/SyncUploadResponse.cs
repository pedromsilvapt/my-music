using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record SyncUploadResponse
{
    public required bool Success { get; init; }
    public long? SongId { get; init; }

    /// <summary>
    /// Every record created for the uploaded file, in order. Besides the upload's own record, this can hold
    /// an <c>UpdateLocal</c> the device must perform, when the file is a previous version of a song.
    /// </summary>
    public required List<SyncRecordResponseItem> Records { get; init; }

    public required SyncActionCounts Counts { get; init; }
}