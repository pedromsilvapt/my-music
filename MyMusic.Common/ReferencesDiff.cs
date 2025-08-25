namespace MyMusic.Common;

public class ReferencesDiff<T, K>
{
    public List<T> Added { get; } = new List<T>();

    public List<T> Removed { get; } = new List<T>();

    public List<T> Kept { get; } = new List<T>();

    public static ReferencesDiff<T, K> From(IEnumerable<T>? oldReferences, IEnumerable<T>? newReferences, Func<T, K> getKey)
    {
        var diffs = new ReferencesDiff<T, K>();

        if (oldReferences is null && newReferences is not null)
        {
            diffs.Added.AddRange(newReferences);
        }
        else if (oldReferences is not null && newReferences is null)
        {
            diffs.Removed.AddRange(oldReferences);
        }
        else if (oldReferences is not null && newReferences is not null)
        {
            // Prevent double enumeration
            oldReferences = oldReferences.ToList();
            
            var oldReferenceKeys = new HashSet<K>();

            var newReferenceKeys = new HashSet<K>();

            foreach (var oldRef in oldReferences)
            {
                var primaryKey = getKey(oldRef);

                oldReferenceKeys.Add(primaryKey);
            }

            foreach (var newRef in newReferences)
            {
                var primaryKey = getKey(newRef);

                newReferenceKeys.Add(primaryKey);

                if (oldReferenceKeys.Contains(primaryKey))
                {
                    diffs.Kept.Add(newRef);
                }
                else
                {
                    diffs.Added.Add(newRef);
                }
            }

            foreach (var oldRef in oldReferences)
            {
                var primaryKey = getKey(oldRef);

                if (!newReferenceKeys.Contains(primaryKey))
                {
                    diffs.Removed.Add(oldRef);
                }
            }
        }

        return diffs;
    }
}

public static class ReferencesDiff
{
    public static ReferencesDiff<T, K> From<T, K>(IEnumerable<T>? oldReferences, IEnumerable<T>? newReferences, Func<T, K> getKey)
    {
        return ReferencesDiff<T, K>.From(oldReferences, newReferences, getKey);
    }
}