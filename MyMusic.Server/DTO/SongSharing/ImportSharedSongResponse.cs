using MyMusic.Common.Services;

namespace MyMusic.Server.DTO.SongSharing;

/// <summary>
/// Response for <c>POST songs/{songId}/import</c> (Phase 7 — Import Shared Song).
/// Mapped from the Common-layer <see cref="ImportSharedSongResult"/>.
/// </summary>
public record ImportSharedSongResponse
{
    /// <summary>Whether the import attempt completed (freshly imported or already present).</summary>
    public bool Success { get; set; }

    /// <summary>The Id of the song in the recipient's library, or null on failure.</summary>
    public long? SongId { get; set; }

    /// <summary>
    /// True when the recipient already owned a copy of this song (same checksum) and the
    /// <c>Skip</c> duplicate strategy kept the existing row instead of creating a new one.
    /// </summary>
    public bool AlreadyImported { get; set; }

    /// <summary>Human-readable error message when <see cref="Success"/> is false.</summary>
    public string? Error { get; set; }

    public static ImportSharedSongResponse FromResult(ImportSharedSongResult result) =>
        new()
        {
            Success = result.Success,
            SongId = result.SongId,
            AlreadyImported = result.AlreadyImported,
            Error = result.Error,
        };
}