using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyMusic.Common.Filters;

public static class DynamicFilterBuilder
{
    public static void ResolveEntityPaths(FilterRequest request, Dictionary<string, string> fieldMappings)
    {
        ResolveEntityPathsInRules(request.Rules, fieldMappings);
    }

    private static void ResolveEntityPathsInRules(List<FilterRule> rules, Dictionary<string, string> fieldMappings)
    {
        foreach (var rule in rules)
        {
            switch (rule)
            {
                case FilterConditionRule condition:
                {
                    var fieldKey = StripQuantifiers(condition.Field);
                    if (!fieldMappings.TryGetValue(fieldKey, out var entityPath))
                    {
                        continue;
                    }

                    var quantifiers = ExtractQuantifiersWithPositions(condition.Field);
                    condition.EntityPath = quantifiers.Count > 0
                        ? ApplyQuantifiersToEntityPath(entityPath, quantifiers)
                        : entityPath;
                    break;
                }
                case FilterGroupRule group:
                    ResolveEntityPathsInRules(group.Rules, fieldMappings);
                    break;
            }
        }
    }

    private static string StripQuantifiers(string field) => Regex.Replace(field, @"\[(any|all)\]", "");

    private static List<(int segmentIndex, string quantifier)> ExtractQuantifiersWithPositions(string field)
    {
        var result = new List<(int, string)>();
        var segments = field.Split('.');

        for (var i = 0; i < segments.Length; i++)
        {
            var match = Regex.Match(segments[i], @"\[(any|all)\]");
            if (match.Success)
            {
                result.Add((i, match.Value));
            }
        }

        return result;
    }

    private static string ApplyQuantifiersToEntityPath(string entityPath,
        List<(int segmentIndex, string quantifier)> quantifiers)
    {
        var segments = entityPath.Split('.');

        foreach (var (index, quantifier) in quantifiers)
        {
            if (index < segments.Length)
            {
                segments[index] += quantifier;
            }
        }

        return string.Join(".", segments);
    }

    public static Expression<Func<T, bool>> BuildFilter<T>(FilterRequest request)
    {
        if (request.Rules.Count == 0)
        {
            return x => true;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var expression = BuildGroupExpression(parameter, request.Rules, request.Combinator);

        return Expression.Lambda<Func<T, bool>>(expression, parameter);
    }

    private static Expression BuildGroupExpression(ParameterExpression parameter, List<FilterRule> rules,
        FilterCombinator combinator)
    {
        if (rules.Count == 0)
        {
            return Expression.Constant(true);
        }

        var expressions = rules.Select(rule => BuildRuleExpression(parameter, rule)).ToList();

        return combinator == FilterCombinator.And
            ? expressions.Aggregate(Expression.AndAlso)
            : expressions.Aggregate(Expression.OrElse);
    }

    private static Expression BuildRuleExpression(ParameterExpression parameter, FilterRule rule)
    {
        if (rule is FilterConditionRule condition)
        {
            return BuildConditionExpression(parameter, condition);
        }

        if (rule is FilterGroupRule group)
        {
            return BuildGroupExpression(parameter, group.Rules, group.Combinator);
        }

        throw new InvalidOperationException($"Unknown rule type: {rule.GetType().Name}");
    }

    private static Expression BuildConditionExpression(ParameterExpression parameter, FilterConditionRule condition)
    {
        var path = condition.EntityPath ?? condition.Field;
        var pathSegments = ParsePathWithQuantifiers(path);
        var expression = BuildPropertyPathExpression(parameter, pathSegments, condition);

        return expression;
    }

    private static List<PathSegment> ParsePathWithQuantifiers(string path)
    {
        var segments = new List<PathSegment>();
        var regex = new Regex(@"(\w+)(?:\[(any|all)\])?");
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            var match = regex.Match(part);
            if (match.Success)
            {
                var propertyName = match.Groups[1].Value;
                var quantifierStr = match.Groups[2].Value;
                FilterQuantifier? quantifier = null;

                if (!string.IsNullOrEmpty(quantifierStr))
                {
                    quantifier = quantifierStr.ToLower() == "any" ? FilterQuantifier.Any : FilterQuantifier.All;
                }

                segments.Add(new PathSegment(propertyName, quantifier));
            }
        }

        return segments;
    }

    private static Expression BuildPropertyPathExpression(
        ParameterExpression parameter,
        List<PathSegment> segments,
        FilterConditionRule condition)
    {
        Expression current = parameter;
        var i = 0;

        while (i < segments.Count)
        {
            var segment = segments[i];
            var propertyName = MapPropertyName(segment.PropertyName, current.Type);

            var property = Expression.Property(current, propertyName);
            var propertyType = property.Type;

            if (IsCollectionType(propertyType, out var elementType))
            {
                var effectiveQuantifier = GetEffectiveQuantifier(segment.Quantifier, condition.Operator);
                var remainingSegments = segments.Skip(i + 1).ToList();

                return BuildCollectionExpression(
                    property,
                    elementType!,
                    remainingSegments,
                    condition,
                    effectiveQuantifier);
            }

            current = property;
            i++;
        }

        return BuildFinalExpression(current, condition);
    }

    private static Expression BuildCollectionExpression(
        Expression collectionProperty,
        Type elementType,
        List<PathSegment> remainingSegments,
        FilterConditionRule condition,
        FilterQuantifier quantifier)
    {
        var elementParam = Expression.Parameter(elementType, "item");

        Expression innerExpression;
        if (remainingSegments.Count == 0)
        {
            innerExpression = BuildFinalExpression(elementParam, condition);
        }
        else
        {
            innerExpression = BuildPropertyPathExpression(elementParam, remainingSegments, condition);
        }

        var lambda = Expression.Lambda(innerExpression, elementParam);

        var methodName = quantifier == FilterQuantifier.All ? "All" : "Any";
        var enumerableMethods = typeof(Enumerable).GetMethods()
            .Where(m => m.Name == methodName && m.GetParameters().Length == 2)
            .First();

        var genericMethod = enumerableMethods.MakeGenericMethod(elementType);

        return Expression.Call(genericMethod, collectionProperty, lambda);
    }

    private static Expression BuildFinalExpression(Expression property, FilterConditionRule condition)
    {
        var propertyType = property.Type;
        var nullableUnderlyingType = Nullable.GetUnderlyingType(propertyType);
        var actualType = nullableUnderlyingType ?? propertyType;

        return condition.Operator switch
        {
            FilterOperator.Eq => BuildComparisonExpression(property, condition.Value, actualType, ExpressionType.Equal),
            FilterOperator.Neq => BuildComparisonExpression(property, condition.Value, actualType,
                ExpressionType.NotEqual),
            FilterOperator.Gt => BuildComparisonExpression(property, condition.Value, actualType,
                ExpressionType.GreaterThan),
            FilterOperator.Gte => BuildComparisonExpression(property, condition.Value, actualType,
                ExpressionType.GreaterThanOrEqual),
            FilterOperator.Lt => BuildComparisonExpression(property, condition.Value, actualType,
                ExpressionType.LessThan),
            FilterOperator.Lte => BuildComparisonExpression(property, condition.Value, actualType,
                ExpressionType.LessThanOrEqual),
            FilterOperator.Contains => BuildStringMethodExpression(property, condition.Value, "Contains"),
            FilterOperator.StartsWith => BuildStringMethodExpression(property, condition.Value, "StartsWith"),
            FilterOperator.EndsWith => BuildStringMethodExpression(property, condition.Value, "EndsWith"),
            FilterOperator.In => BuildInExpression(property, condition.Value, actualType),
            FilterOperator.NotIn => Expression.Not(BuildInExpression(property, condition.Value, actualType)),
            FilterOperator.IsNull => BuildNullExpression(property, true),
            FilterOperator.IsNotNull => BuildNullExpression(property, false),
            FilterOperator.Between => BuildBetweenExpression(property, condition.Value, condition.Value2, actualType),
            FilterOperator.IsTrue => BuildBooleanExpression(property, true),
            FilterOperator.IsFalse => BuildBooleanExpression(property, false),
            _ => throw new InvalidOperationException($"Unsupported operator: {condition.Operator}"),
        };
    }

    private static string MapPropertyName(string fieldName, Type parentType)
    {
        var directProperty = parentType.GetProperty(fieldName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (directProperty != null)
        {
            return directProperty.Name;
        }

        foreach (var prop in parentType.GetProperties())
        {
            if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Name;
            }
        }

        return fieldName;
    }

    private static FilterQuantifier GetEffectiveQuantifier(FilterQuantifier? explicitQuantifier, FilterOperator op)
    {
        if (explicitQuantifier.HasValue)
        {
            return explicitQuantifier.Value;
        }

        return op switch
        {
            FilterOperator.Neq or FilterOperator.NotIn => FilterQuantifier.All,
            _ => FilterQuantifier.Any,
        };
    }

    private static bool IsCollectionType(Type type, out Type? elementType)
    {
        elementType = null;

        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IList<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = iface.GetGenericArguments()[0];
                return true;
            }
        }

        return false;
    }

    private static Expression BuildComparisonExpression(Expression property, object? value, Type propertyType,
        ExpressionType comparisonType)
    {
        if (value == null)
        {
            return comparisonType == ExpressionType.Equal
                ? Expression.Equal(property, Expression.Constant(null))
                : Expression.NotEqual(property, Expression.Constant(null));
        }

        var convertedValue = ConvertValue(value, propertyType);
        Expression constant = Expression.Constant(convertedValue, propertyType);

        if (property.Type != constant.Type)
        {
            constant = Expression.Convert(constant, property.Type);
        }

        return Expression.MakeBinary(comparisonType, property, constant);
    }

    private static Expression BuildStringMethodExpression(Expression property, object? value, string methodName)
    {
        if (value == null)
        {
            return Expression.Constant(false);
        }

        var stringValue = value.ToString() ?? "";
        var lowerValue = stringValue.ToLower();

        var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), [])
                            ?? throw new InvalidOperationException("ToLower method not found");

        var method = typeof(string).GetMethod(methodName, [typeof(string)])
                     ?? throw new InvalidOperationException($"Method {methodName} not found on string");

        var nullCheck = Expression.NotEqual(property, Expression.Constant(null));
        var lowerProperty = Expression.Call(property, toLowerMethod);
        var constant = Expression.Constant(lowerValue);
        var methodCall = Expression.Call(lowerProperty, method, constant);

        return Expression.AndAlso(nullCheck, methodCall);
    }

    private static Expression BuildInExpression(Expression property, object? value, Type propertyType)
    {
        if (value == null)
        {
            return Expression.Constant(false);
        }

        var values = value is JsonElement jsonElement
            ? jsonElement.EnumerateArray().Select(e => ConvertValue(GetElementValue(e), propertyType) ?? "").ToList()
            : value as IEnumerable<object> ?? [value];

        var list = values.ToList()!;
        var constant = Expression.Constant(list, typeof(List<object>));

        var containsMethod = typeof(List<object>).GetMethod("Contains", [typeof(object)])
                             ?? throw new InvalidOperationException("Contains method not found");

        var boxedProperty = Expression.Convert(property, typeof(object));
        return Expression.Call(constant, containsMethod, boxedProperty);
    }

    private static Expression BuildNullExpression(Expression property, bool isNull) =>
        isNull
            ? Expression.Equal(property, Expression.Constant(null))
            : Expression.NotEqual(property, Expression.Constant(null));

    private static Expression BuildBetweenExpression(Expression property, object? value1, object? value2,
        Type propertyType)
    {
        if (value1 == null || value2 == null)
        {
            return Expression.Constant(false);
        }

        var convertedValue1 = ConvertValue(value1, propertyType);
        var convertedValue2 = ConvertValue(value2, propertyType);

        Expression constant1 = Expression.Constant(convertedValue1, propertyType);
        Expression constant2 = Expression.Constant(convertedValue2, propertyType);

        if (property.Type != constant1.Type)
        {
            constant1 = Expression.Convert(constant1, property.Type);
            constant2 = Expression.Convert(constant2, property.Type);
        }

        var lowerBound = Expression.MakeBinary(
            ExpressionType.GreaterThanOrEqual,
            property,
            constant1);

        var upperBound = Expression.MakeBinary(
            ExpressionType.LessThanOrEqual,
            property,
            constant2);

        return Expression.AndAlso(lowerBound, upperBound);
    }

    private static Expression BuildBooleanExpression(Expression property, bool expectedValue) =>
        Expression.Equal(property, Expression.Constant(expectedValue));

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        if (value is JsonElement jsonElement)
        {
            value = GetElementValue(jsonElement);
        }

        if (value == null)
        {
            return null;
        }

        var sourceType = value.GetType();

        if (targetType == sourceType)
        {
            return value;
        }

        if (targetType == typeof(string))
        {
            return value.ToString();
        }

        if (targetType == typeof(int))
        {
            return Convert.ToInt32(value);
        }

        if (targetType == typeof(long))
        {
            return Convert.ToInt64(value);
        }

        if (targetType == typeof(double))
        {
            return Convert.ToDouble(value);
        }

        if (targetType == typeof(decimal))
        {
            return Convert.ToDecimal(value);
        }

        if (targetType == typeof(bool))
        {
            return Convert.ToBoolean(value);
        }

        if (targetType == typeof(DateTime))
        {
            return Convert.ToDateTime(value);
        }

        if (targetType == typeof(TimeSpan))
        {
            return TimeSpan.Parse(value.ToString()!);
        }

        return Convert.ChangeType(value, targetType);
    }

    private static object? GetElementValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString(),
        };
    }

    private record PathSegment(string PropertyName, FilterQuantifier? Quantifier);
}