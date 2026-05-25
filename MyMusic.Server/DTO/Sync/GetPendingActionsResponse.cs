using System.Text.Json;
using MyMusic.Common.Entities;

namespace MyMusic.Server.DTO.Sync;

public record CreatePendingActionsResponse
{
    public required List<SyncRecordResponseItem> Records { get; init; }
}