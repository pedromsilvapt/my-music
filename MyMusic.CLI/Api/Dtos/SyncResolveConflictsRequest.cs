namespace MyMusic.CLI.Api.Dtos;

public record SyncConflictResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    public required string Checksum { get; init; }
    public required string ChecksumAlgorithm { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
}

public record SyncPotentialUpdateResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    public required string Checksum { get; init; }
    public required string ChecksumAlgorithm { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
    public required DateTime LastSyncedAt { get; init; }
}

public record SyncResolveConflictsRequest
{
    public required List<SyncConflictResolveItem> Conflicts { get; init; }
    public required List<SyncPotentialUpdateResolveItem> PotentialUpdates { get; init; }
}