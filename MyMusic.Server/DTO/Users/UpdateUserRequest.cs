using System.Text.Json.Serialization;

namespace MyMusic.Server.DTO.Users;

public record UpdateUserRequest
{
    [JsonPropertyName("colorScheme")] public string? ColorScheme { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("volume")] public double? Volume { get; set; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; set; }
    [JsonPropertyName("autoDownloadOnPurchase")] public bool? AutoDownloadOnPurchase { get; set; }
    [JsonPropertyName("currentQueueId")] public long? CurrentQueueId { get; set; }
}