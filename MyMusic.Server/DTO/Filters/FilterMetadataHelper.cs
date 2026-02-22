namespace MyMusic.Server.DTO.Filters;

public static class FilterMetadataHelper
{
    public static List<FilterOperatorMetadata> GetOperatorMetadata() =>
    [
        new()
        {
            Name = "eq", DisplayName = "Equals", Description = "Equal to value (= or ==)",
            ApplicableTypes = ["string", "number", "boolean", "date"],
        },
        new()
        {
            Name = "neq", DisplayName = "Not Equals", Description = "Not equal to value (!= or <>)",
            ApplicableTypes = ["string", "number", "boolean", "date"],
        },
        new()
        {
            Name = "gt", DisplayName = "Greater Than", Description = "Greater than (>)",
            ApplicableTypes = ["number", "date"],
        },
        new()
        {
            Name = "gte", DisplayName = "Greater Than or Equal", Description = "Greater than or equal (>=)",
            ApplicableTypes = ["number", "date"],
        },
        new()
        {
            Name = "lt", DisplayName = "Less Than", Description = "Less than (<)", ApplicableTypes = ["number", "date"],
        },
        new()
        {
            Name = "lte", DisplayName = "Less Than or Equal", Description = "Less than or equal (<=)",
            ApplicableTypes = ["number", "date"],
        },
        new()
        {
            Name = "contains", DisplayName = "Contains", Description = "String contains value (~ or contains)",
            ApplicableTypes = ["string"],
        },
        new()
        {
            Name = "startsWith", DisplayName = "Starts With", Description = "String starts with value",
            ApplicableTypes = ["string"],
        },
        new()
        {
            Name = "endsWith", DisplayName = "Ends With", Description = "String ends with value",
            ApplicableTypes = ["string"],
        },
        new()
        {
            Name = "in", DisplayName = "In", Description = "Value is in list", ApplicableTypes = ["string", "number"],
        },
        new()
        {
            Name = "notIn", DisplayName = "Not In", Description = "Value is not in list",
            ApplicableTypes = ["string", "number"],
        },
        new()
        {
            Name = "isNull", DisplayName = "Is Null", Description = "Value is null",
            ApplicableTypes = ["string", "number", "date"],
        },
        new()
        {
            Name = "isNotNull", DisplayName = "Is Not Null", Description = "Value is not null",
            ApplicableTypes = ["string", "number", "date"],
        },
        new()
        {
            Name = "between", DisplayName = "Between", Description = "Value is between two values",
            ApplicableTypes = ["number", "date"],
        },
        new()
        {
            Name = "isTrue", DisplayName = "Is True", Description = "Boolean is true", ApplicableTypes = ["boolean"],
        },
        new()
        {
            Name = "isFalse", DisplayName = "Is False", Description = "Boolean is false", ApplicableTypes = ["boolean"],
        },
    ];
}