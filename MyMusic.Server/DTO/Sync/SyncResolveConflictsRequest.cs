namespace MyMusic.Server.DTO.Sync;

public record SyncConflictResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    /// <summary>Base64 checksum of the local file, computed by the client.</summary>
    public string? Checksum { get; init; }
    /// <summary>Algorithm used for <see cref="Checksum"/>. Required when it is set.</summary>
    public string? ChecksumAlgorithm { get; init; }
    /// <summary>Whole local file, sent by legacy clients that do not compute the checksum themselves.</summary>
    public string? FileContentBase64 { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
}

public record SyncPotentialUpdateResolveItem
{
    public required string Path { get; init; }
    public required long SongId { get; init; }
    /// <summary>Base64 checksum of the local file, computed by the client.</summary>
    public string? Checksum { get; init; }
    /// <summary>Algorithm used for <see cref="Checksum"/>. Required when it is set.</summary>
    public string? ChecksumAlgorithm { get; init; }
    /// <summary>Whole local file, sent by legacy clients that do not compute the checksum themselves.</summary>
    public string? FileContentBase64 { get; init; }
    public required DateTime LocalModifiedAt { get; init; }
    public required DateTime LastSyncedAt { get; init; }
}

public record SyncResolveConflictsRequest
{
    public required List<SyncConflictResolveItem> Conflicts { get; init; }
    public required List<SyncPotentialUpdateResolveItem> PotentialUpdates { get; init; }
}