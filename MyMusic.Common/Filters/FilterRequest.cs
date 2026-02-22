using System.Text.Json.Serialization;

namespace MyMusic.Common.Filters;

public class FilterRequest
{
    [JsonPropertyName("combinator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FilterCombinator Combinator { get; set; } = FilterCombinator.And;

    [JsonPropertyName("rules")] public List<FilterRule> Rules { get; set; } = [];

    public static FilterRequest Empty() => new();
}