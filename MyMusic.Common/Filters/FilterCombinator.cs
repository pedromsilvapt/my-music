using System.Text.Json.Serialization;

namespace MyMusic.Common.Filters;

public enum FilterCombinator
{
    [JsonPropertyName("and")] And,

    [JsonPropertyName("or")] Or,
}