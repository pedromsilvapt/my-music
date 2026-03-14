namespace MyMusic.CLI.Api.Dtos;

public record SyncConflictErrorItem
{
    public required string Path { get; init; }
    public required string Reason { get; init; }
}

public record SyncResolveConflictsResponse
{
    public required List<SyncFileInfoItem> ToUpload { get; init; }
    public required List<SyncConflictErrorItem> Conflicts { get; init; }
}