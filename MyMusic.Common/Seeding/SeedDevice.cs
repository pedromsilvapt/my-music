using System.Text.Json.Serialization;

namespace MyMusic.Common.Seeding;

public class SeedDevice
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("namingTemplate")]
    public string? NamingTemplate { get; set; }
}