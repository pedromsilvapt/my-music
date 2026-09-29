using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Sync;

public enum SyncUploadActionType
{
    CreateRemote,
    UpdateRemote,
    LinkWithSongId,
    LinkWithChecksumOnly,

    /// <summary>The updated file is a previous version of its own song: the device downloads the current one.</summary>
    UpdateLocal,

    /// <summary>The file is a previous version of another song: it is linked to it, and the device downloads its current file.</summary>
    LinkWithSongIdAndUpdateLocal,
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
    /// <summary>The records created for the uploaded file, in order.</summary>
    public required List<DeviceSyncSessionRecord> Records { get; init; }

    public long? EffectiveSongId { get; init; }
}