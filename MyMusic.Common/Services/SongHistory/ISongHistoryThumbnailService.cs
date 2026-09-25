namespace MyMusic.Common.Services.SongHistory;

public interface ISongHistoryThumbnailService
{
    /// <summary>
    /// Generates a 265x265 (max, aspect-preserving) thumbnail from base64 artwork data.
    /// Returns null if generation fails (graceful degradation — the history entry
    /// will still be written without a thumbnail).
    /// </summary>
    byte[]? GenerateThumbnail(string base64Data, string mimeType);
}