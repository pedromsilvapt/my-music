using System.Linq.Expressions;

namespace MyMusic.Common.Filters;

public static class FuzzySearchHelper
{
    public static IQueryable<T> ApplyFuzzySearch<T>(
        IQueryable<T> query,
        string? search,
        Expression<Func<T, string>> searchableTextSelector)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var searchTerms = search
            .ToLower()
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (searchTerms.Length == 0)
        {
            return query;
        }

        var parameter = searchableTextSelector.Parameters[0];
        var body = searchableTextSelector.Body;

        Expression? combinedExpression = null;

        foreach (var term in searchTerms)
        {
            var termExpression = BuildContainsExpression(parameter, body, term);

            if (combinedExpression == null)
            {
                combinedExpression = termExpression;
            }
            else
            {
                combinedExpression = Expression.AndAlso(combinedExpression, termExpression);
            }
        }

        var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression!, parameter);
        return query.Where(lambda);
    }

    private static Expression BuildContainsExpression(
        ParameterExpression parameter,
        Expression selectorBody,
        string searchTerm)
    {
        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var toLowerCall = Expression.Call(selectorBody, toLowerMethod);

        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;
        var searchTermConstant = Expression.Constant(searchTerm);

        return Expression.Call(toLowerCall, containsMethod, searchTermConstant);
    }
}