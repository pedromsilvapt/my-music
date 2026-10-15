namespace MyMusic.Common.Sources.Queries;

/// <summary>
///     Marks a property as a <c>key:value</c> query modifier, accepted under any of the given labels (case-insensitive)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class QueryParamAttribute(params string[] labels) : Attribute
{
    public IReadOnlyList<string> Labels { get; } = labels;
}

/// <summary>
///     Marks the <see cref="string" /> property that receives the free text left after removing the recognized modifiers
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class QueryTextAttribute : Attribute;
