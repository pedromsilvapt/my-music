namespace MyMusic.Server.DTO.Sync;

public record SyncCheckResponse
{
    public required List<SyncFileInfoItem> ToCreate { get; init; }
    public required List<SyncFileInfoItem> ToUpdate { get; init; }
    public required List<SyncPotentialConflictItem> PotentialConflicts { get; init; }
}

public record SyncPotentialConflictItem
{
    public required string Path { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
    public required DateTime ServerModifiedAt { get; init; }
    public required DateTime? LastSyncedAt { get; init; }
    public required long SongId { get; init; }
    public required string ServerChecksum { get; init; }
    public required string ServerChecksumAlgorithm { get; init; }
}