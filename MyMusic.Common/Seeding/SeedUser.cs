using System.Text.Json.Serialization;

namespace MyMusic.Common.Seeding;

public class SeedUser
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("autoDownloadOnPurchase")]
    public bool AutoDownloadOnPurchase { get; set; } = false;

    [JsonPropertyName("devices")]
    public List<SeedDevice>? Devices { get; set; }
}