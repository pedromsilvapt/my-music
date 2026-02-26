using System.Text.Json.Serialization;

namespace MyMusic.Common.Seeding;

public class SeedSource
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("icon")]
    public required string Icon { get; set; }

    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonPropertyName("isPaid")]
    public bool IsPaid { get; set; }
}