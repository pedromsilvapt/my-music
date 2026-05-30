using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public enum SyncUploadActionType
{
    CreateRemote,
    UpdateRemote,
    LinkWithSongId,
    LinkWithChecksumOnly,
}

public record SyncUploadDecision
{
    public required SyncUploadActionType ActionType { get; init; }
    public long? SongId { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumAlgorithm { get; init; }
    public string? Reason { get; init; }
}

public record SyncUploadResult
{
    public required DeviceSyncSessionRecord Record { get; init; }
    public long? EffectiveSongId { get; init; }
}