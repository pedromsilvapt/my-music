namespace MyMusic.CLI.Api.Dtos;

public record SyncUploadResponse
{
    public required bool Success { get; init; }
    public long? SongId { get; init; }

    /// <summary>
    /// Every record the server created for the uploaded file, in order. Can include an <c>UpdateLocal</c> the
    /// device must perform, when the file is a previous version of a song.
    /// </summary>
    public List<SyncRecordResponseItem> Records { get; init; } = [];

    public required SyncActionCounts Counts { get; init; }
}