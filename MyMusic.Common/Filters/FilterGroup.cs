using System.Text.Json.Serialization;

namespace MyMusic.Common.Filters;

public class FilterGroup
{
    [JsonPropertyName("combinator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FilterCombinator Combinator { get; set; } = FilterCombinator.And;

    [JsonPropertyName("rules")] public List<FilterRule> Rules { get; set; } = [];
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(FilterCondition), "condition")]
[JsonDerivedType(typeof(FilterGroup), "group")]
public abstract class FilterRule { }

public class FilterConditionRule : FilterRule
{
    [JsonPropertyName("field")] public required string Field { get; set; }

    [JsonIgnore] public string? EntityPath { get; set; }

    [JsonPropertyName("operator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FilterOperator Operator { get; set; }

    [JsonPropertyName("value")] public object? Value { get; set; }

    [JsonPropertyName("value2")] public object? Value2 { get; set; }

    [JsonPropertyName("quantifier")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FilterQuantifier? Quantifier { get; set; }
}

public class FilterGroupRule : FilterRule
{
    [JsonPropertyName("combinator")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FilterCombinator Combinator { get; set; } = FilterCombinator.And;

    [JsonPropertyName("rules")] public List<FilterRule> Rules { get; set; } = [];
}