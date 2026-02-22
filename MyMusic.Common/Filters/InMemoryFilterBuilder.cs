namespace MyMusic.Common.Filters;

public static class InMemoryFilterBuilder
{
    public static IEnumerable<T> ApplyFilter<T>(IEnumerable<T> items, FilterRequest request)
    {
        if (request.Rules.Count == 0)
        {
            return items;
        }

        var expression = DynamicFilterBuilder.BuildFilter<T>(request);
        var compiled = expression.Compile();
        return items.Where(compiled);
    }

    public static IEnumerable<T> ApplyFuzzySearch<T>(
        IEnumerable<T> items,
        string? search,
        Func<T, string> searchableTextSelector)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return items;
        }

        var searchTerms = search.ToLower()
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (searchTerms.Length == 0)
        {
            return items;
        }

        return items.Where(item =>
        {
            var text = searchableTextSelector(item).ToLower();
            return searchTerms.All(term => text.Contains(term));
        });
    }
}