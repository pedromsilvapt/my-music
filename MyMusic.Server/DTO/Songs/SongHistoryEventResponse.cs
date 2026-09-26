using MyMusic.Common.Services.Songs;

namespace MyMusic.Server.DTO.Songs;

/// <summary>
/// Payload of a song history Server-Sent Event. The SSE event type is <c>processed</c> or
/// <c>complete</c> (see <see cref="SongHistoryEventType"/>).
/// </summary>
public record SongHistoryEventResponse
{
    public const string ProcessedEventType = "processed";
    public const string CompleteEventType = "complete";

    public required long SongId { get; init; }

    public static SongHistoryEventResponse FromEvent(SongHistoryEvent evt) =>
        new()
        {
            SongId = evt.SongId,
        };

    public static string EventTypeOf(SongHistoryEvent evt) =>
        evt.Type switch
        {
            SongHistoryEventType.Processed => ProcessedEventType,
            SongHistoryEventType.Complete => CompleteEventType,
            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt.Type, "Unknown song history event type"),
        };
}
