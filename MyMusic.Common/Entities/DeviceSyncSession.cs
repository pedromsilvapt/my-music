using System.ComponentModel.DataAnnotations;

namespace MyMusic.Common.Entities;

public class DeviceSyncSession
{
    public long Id { get; set; }

    public Device Device { get; set; } = null!;
    public long DeviceId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public SyncSessionStatus Status { get; set; }

    public bool IsDryRun { get; set; }

    /// <summary>
    /// Direction of the sync, chosen by the client when the session starts. Every step of the
    /// session (check, resolve-conflicts, commit) reads it from here so records are always
    /// produced under a single, consistent direction.
    /// </summary>
    public SyncDirection Direction { get; set; } = SyncDirection.Both;

    public string? RepositoryPath { get; set; }

    /// <summary>
    /// When set, files that would be created on the server (<c>CreateRemote</c>) are first matched by
    /// acoustic fingerprint (soundalike) against the user's library and this session's other uploads.
    /// See docs/development/sync.md, "Soundalike Deduplication".
    /// </summary>
    public bool Deduplicate { get; set; }

    /// <summary>
    /// Naming template resolved when the session starts: the device's template, or the one the client
    /// sent to preview in a dry run. Every step of the session (check, resolve-conflicts,
    /// pending-actions) reads it from here so paths are always computed with a single template.
    /// <c>null</c> only for sessions started before this was recorded.
    /// </summary>
    [MaxLength(512)]
    public string? NamingTemplate { get; set; }

    /// <summary>
    /// When set, the session's <c>Skipped</c> records are kept after it completes. By default they are
    /// deleted on completion (most files of a sync are unchanged) and only counted in
    /// <see cref="DeletedSkippedCount"/>. See docs/development/sync.md, "Skipped Records".
    /// </summary>
    public bool RecordSkipped { get; set; }

    /// <summary>
    /// Number of <c>Skipped</c> records deleted when the session completed. The session's skipped total
    /// is this plus the <c>Skipped</c> records still stored.
    /// </summary>
    public int DeletedSkippedCount { get; set; }

    public List<DeviceSyncSessionRecord> Records { get; set; } = [];
}

public enum SyncSessionStatus
{
    InProgress,
    Committed,
    Completed,
    Cancelled,
}

/// <summary>
/// Direction of a sync session. See docs/development/sync.md, "Sync Direction".
/// </summary>
public enum SyncDirection
{
    /// <summary>Push and pull in the same session.</summary>
    Both,

    /// <summary>Push only: the device is the source of truth for this session.</summary>
    Up,

    /// <summary>Pull only: the server is the source of truth for this session.</summary>
    Down,
}
