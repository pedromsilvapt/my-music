namespace MyMusic.Server.DTO.Sync;

public record SyncConflictChoicesRequest
{
    /// <summary>Ids of the <c>Conflict</c> records the user resolved by downloading the server's version.</summary>
    public required List<long> DownloadRecordIds { get; init; }
}
