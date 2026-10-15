using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MyMusic.Common.Sources.Queries;

/// <summary>
///     Parses search queries with inline <c>key:value</c> modifiers, such as <c>Nemo artist:UCQplaZZ2mv8fo8Zd-1hQPzg</c>.
///     Tokens with an unknown key or an invalid value are kept as free text, so titles like "Re:Zero" still work.
/// </summary>
public static partial class QueryParser
{
    private sealed record Metadata(IReadOnlyDictionary<string, PropertyInfo> Params, PropertyInfo? Text);

    private static readonly ConcurrentDictionary<Type, Metadata> MetadataCache = new();

    [GeneratedRegex(@"^([A-Za-z]+):(\S+)$")]
    private static partial Regex ModifierRegex();

    [GeneratedRegex(@"^[A-Za-z]+$")]
    private static partial Regex KeyRegex();

    /// <summary>
    ///     Parses the query into a <typeparamref name="T" />, setting each <see cref="QueryParamAttribute" /> property
    ///     from its modifier (last one wins), and the <see cref="QueryTextAttribute" /> property from the remaining tokens
    /// </summary>
    public static T Parse<T>(string query) where T : new()
    {
        var metadata = MetadataCache.GetOrAdd(typeof(T), BuildMetadata);
        var result = new T();
        var text = new List<string>();

        foreach (var token in Tokenize(query))
        {
            var match = ModifierRegex().Match(token);

            if (match.Success
                && metadata.Params.TryGetValue(match.Groups[1].Value, out var property)
                && TryConvert(property.PropertyType, match.Groups[2].Value, out var value))
            {
                property.SetValue(result, value);
            }
            else
            {
                text.Add(token);
            }
        }

        metadata.Text?.SetValue(result, string.Join(' ', text));

        return result;
    }

    /// <summary>
    ///     Sets the <c>key:value</c> modifier in the query: the first token with that key (case-insensitive) is replaced
    ///     in place and any others are removed, or it is appended when missing. A null or empty value removes it.
    ///     Every other token is kept as is.
    /// </summary>
    public static string Upsert(string query, string key, string? value)
    {
        if (!KeyRegex().IsMatch(key))
        {
            throw new ArgumentException($"Invalid query key \"{key}\"", nameof(key));
        }

        if (value is not null && value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"Invalid query value \"{value}\"", nameof(value));
        }

        var modifier = string.IsNullOrEmpty(value) ? null : $"{key}:{value}";
        var tokens = new List<string>();
        var found = false;

        foreach (var token in Tokenize(query))
        {
            var match = ModifierRegex().Match(token);

            if (!match.Success || !match.Groups[1].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(token);
            }
            else if (!found)
            {
                found = true;

                if (modifier is not null)
                {
                    tokens.Add(modifier);
                }
            }
        }

        if (!found && modifier is not null)
        {
            tokens.Add(modifier);
        }

        return string.Join(' ', tokens);
    }

    /// <summary>
    ///     Removes every <c>key:value</c> modifier from the query, whatever its key, returning the free text of any
    ///     source's query. Unlike <see cref="Parse{T}" />, tokens like "Re:Zero" are removed too.
    /// </summary>
    public static string RemoveModifiers(string query) =>
        string.Join(' ', Tokenize(query).Where(token => !ModifierRegex().IsMatch(token)));

    private static string[] Tokenize(string query) =>
        query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static bool TryConvert(Type type, string value, out object? result)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string))
        {
            result = value;
            return true;
        }

        if (type == typeof(bool))
        {
            var success = bool.TryParse(value, out var b);
            result = b;
            return success;
        }

        if (type.IsEnum)
        {
            // Enum.TryParse also accepts numbers, which aren't valid labels
            if (!long.TryParse(value, out _)
                && Enum.TryParse(type, value, ignoreCase: true, out result)
                && Enum.IsDefined(type, result!))
            {
                return true;
            }

            result = null;
            return false;
        }

        throw new NotSupportedException($"Unsupported query param type {type}");
    }

    private static Metadata BuildMetadata(Type type)
    {
        var parameters = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        PropertyInfo? text = null;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<QueryTextAttribute>() is not null)
            {
                if (text is not null || property.PropertyType != typeof(string))
                {
                    throw new InvalidOperationException(
                        $"{type} must have at most one string [QueryText] property, found {property.Name}");
                }

                text = property;
            }

            if (property.GetCustomAttribute<QueryParamAttribute>() is not { } param)
            {
                continue;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (propertyType != typeof(string) && propertyType != typeof(bool) && !propertyType.IsEnum)
            {
                throw new InvalidOperationException(
                    $"Unsupported type {property.PropertyType} for query param {type}.{property.Name}");
            }

            foreach (var label in param.Labels)
            {
                if (!KeyRegex().IsMatch(label))
                {
                    throw new InvalidOperationException($"Invalid query label \"{label}\" on {type}.{property.Name}");
                }

                if (!parameters.TryAdd(label, property))
                {
                    throw new InvalidOperationException(
                        $"Duplicate query label \"{label}\" on {type}.{property.Name} and {type}.{parameters[label].Name}");
                }
            }
        }

        return new Metadata(parameters, text);
    }
}
