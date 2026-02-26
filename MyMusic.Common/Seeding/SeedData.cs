using System.Text.Json.Serialization;

namespace MyMusic.Common.Seeding;

public class SeedData
{
    [JsonPropertyName("users")]
    public List<SeedUser>? Users { get; set; }

    [JsonPropertyName("sources")]
    public List<SeedSource>? Sources { get; set; }
}