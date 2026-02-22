using System.Text.Json.Serialization;

namespace MyMusic.Common.Filters;

public class FilterCondition
{
    [JsonPropertyName("field")] public required string Field { get; set; }

    [JsonPropertyName("operator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FilterOperator Operator { get; set; }

    [JsonPropertyName("value")] public object? Value { get; set; }

    [JsonPropertyName("value2")] public object? Value2 { get; set; }
}