namespace MyMusic.CLI.Api.Dtos;

public record SyncConflictChoicesRequest
{
    public required List<long> DownloadRecordIds { get; init; }
}
