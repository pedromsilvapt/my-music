using System.Text.Json;
using MyMusic.Common.Services.SongHistory.Models;
using Entities = MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Songs;

public record GetSongHistoryResponse
{
    public required List<GetSongHistoryItem> History { get; init; }
}

public record GetSongHistoryItem
{
    public required long Id { get; init; }
    public required long SongId { get; init; }
    public required int SongRevision { get; init; }
    public required JsonElement Diff { get; init; }
    public required DateTime CreatedAt { get; init; }

    public static GetSongHistoryItem FromEntity(Entities.SongHistory history) =>
        new()
        {
            Id = history.Id,
            SongId = history.SongId,
            SongRevision = history.SongRevision,
            Diff = JsonSerializer.SerializeToElement(history.Diff, SongHistoryJsonOptions.Options),
            CreatedAt = history.CreatedAt,
        };
}