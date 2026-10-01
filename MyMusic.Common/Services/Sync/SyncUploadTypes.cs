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

    /// <summary>
    /// The new file sounds like a library song, or like a file uploaded earlier in the session (soundalike
    /// deduplication): it is linked to that song, and the device replaces it with the song's file.
    /// </summary>
    SoundalikeLink,
}

public record SyncUploadDecision
{
    public required SyncUploadActionType ActionType { get; init; }
    public long? SongId { get; init; }
    public string? Checksum { get; init; }
    public string? ChecksumAlgorithm { get; init; }
    public string? Reason { get; init; }

    /// <summary>Checksum of the uploaded file itself, when <see cref="Checksum"/> is another file's (soundalikes).</summary>
    public string? LocalChecksum { get; init; }

    /// <summary>Device path of the session upload a soundalike of it matched, which the device copies.</summary>
    public string? LocalSourcePath { get; init; }
}

public record SyncUploadResult
{
    /// <summary>The records created for the uploaded file, in order.</summary>
    public required List<DeviceSyncSessionRecord> Records { get; init; }

    public long? EffectiveSongId { get; init; }
}