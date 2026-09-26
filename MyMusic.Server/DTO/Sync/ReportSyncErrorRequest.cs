namespace MyMusic.Server.DTO.Sync;

public record ReportSyncErrorRequest
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
    public long? SongId { get; init; }
    /// <summary>
    /// Indicates if this error ocurred as part of the processing of a previously reported action
    /// </summary>
    public long? RecordId { get; init; }
}
