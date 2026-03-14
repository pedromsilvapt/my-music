namespace MyMusic.Server.DTO.Sync;

public record SyncConflictResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    public required string FileContentBase64 { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
}

public record SyncResolveConflictsRequest
{
    public required List<SyncConflictResolveItem> Conflicts { get; init; }
}