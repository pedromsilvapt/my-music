using System.Text.Json.Serialization;

namespace MyMusic.Server.DTO.Users;

public record UpdateUserRequest
{
    [JsonPropertyName("colorScheme")] public string? ColorScheme { get; set; }
}