using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record SyncStartRequest
{
    public bool DryRun { get; init; }

    /// <summary>
    /// Direction of the sync. Stored on the session and used by every step. Defaults to
    /// <see cref="SyncDirection.Both"/>.
    /// </summary>
    public SyncDirection? Direction { get; init; }
    public string? RepositoryPath { get; init; }
    public List<SyncScanErrorItem>? ScanErrors { get; init; }
}

public record SyncScanErrorItem
{
    public required string FilePath { get; init; }
    public required string ErrorMessage { get; init; }
}

public record SyncStartResponse
{
    public required long SessionId { get; init; }
}