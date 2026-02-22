namespace MyMusic.Server.DTO.Filters;

public record FilterMetadataResponse
{
    public required List<FilterFieldMetadata> Fields { get; init; }
    public required List<FilterOperatorMetadata> Operators { get; init; }
}

public record FilterFieldMetadata
{
    public required string Name { get; init; }
    public string? EntityPath { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required List<string> SupportedOperators { get; init; }
    public bool IsComputed { get; init; }
    public bool IsCollection { get; init; }
    public List<FilterFieldMetadata>? NestedFields { get; init; }
    public List<string>? Values { get; init; }
    public bool SupportsDynamicValues { get; init; }
}

public record FilterValuesResponse
{
    public required List<string> Values { get; init; }
}

public record FilterOperatorMetadata
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required List<string> ApplicableTypes { get; init; }
}