namespace MyMusic.Common.Services;

/// <summary>
/// Result of importing a shared song into the recipient's own library via
/// <see cref="ISharedSongImportService.ImportAsync"/>. The Server DTO
/// <c>ImportSharedSongResponse</c> maps directly from this record.
/// </summary>
public record ImportSharedSongResult
{
    /// <summary>
    /// Whether the import attempt completed (the song is now in the recipient's library,
    /// either freshly imported or already present).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The Id of the song in the recipient's library (newly created or pre-existing).
    /// Null when <see cref="Success"/> is false.
    /// </summary>
    public long? SongId { get; init; }

    /// <summary>
    /// True when the recipient already owned a copy of this song (same checksum) and the
    /// <c>Skip</c> duplicate strategy kept the existing row instead of creating a new one.
    /// </summary>
    public bool AlreadyImported { get; init; }

    /// <summary>
    /// Human-readable error message when <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; init; }

    public static ImportSharedSongResult Ok(long songId, bool alreadyImported) =>
        new() { Success = true, SongId = songId, AlreadyImported = alreadyImported };

    public static ImportSharedSongResult Fail(string error) =>
        new() { Success = false, Error = error };
}