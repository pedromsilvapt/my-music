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

    /// <summary>
    /// <c>created</c> for the song's baseline revision (which has no previous values), <c>updated</c> or
    /// <c>deleted</c> otherwise.
    /// </summary>
    public required string Action { get; init; }

    public required JsonElement Diff { get; init; }
    public required DateTime CreatedAt { get; init; }

    public static GetSongHistoryItem FromEntity(Entities.SongHistory history) =>
        new()
        {
            Id = history.Id,
            SongId = history.SongId,
            SongRevision = history.SongRevision,
            Action = history.Action,
            Diff = JsonSerializer.SerializeToElement(history.Diff, SongHistoryJsonOptions.Options),
            CreatedAt = history.CreatedAt,
        };
}