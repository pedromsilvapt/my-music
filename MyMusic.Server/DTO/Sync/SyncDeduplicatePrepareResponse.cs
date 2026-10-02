namespace MyMusic.Server.DTO.Sync;

/// <summary>
/// Progress of fingerprinting the library of a sync session with deduplication: <see cref="Processed"/> of the
/// <see cref="Total"/> songs that had no stored fingerprint. The client calls again until <see cref="Done"/>.
/// </summary>
public record SyncDeduplicatePrepareResponse
{
    public required int Total { get; init; }
    public required int Processed { get; init; }
    public required bool Done { get; init; }
}
