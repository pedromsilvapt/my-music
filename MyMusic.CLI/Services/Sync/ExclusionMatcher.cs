namespace MyMusic.CLI.Services.Sync;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Matches paths relative to the repository against a list of exclusion rules, in a gitignore-like syntax
/// (see "Exclusion Rules" in docs/development/sync.md). Mirrors Mobile's createExclusionMatcher: both are
/// pinned by the same test vectors.
/// </summary>
public class ExclusionMatcher
{
    private readonly List<(string Pattern, Regex Regex)> _rules = [];

    public ExclusionMatcher(IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            var regex = RuleToRegex(pattern);
            if (regex != null)
            {
                _rules.Add((pattern.Trim(), regex));
            }
        }
    }

    /// <summary>The error reported for an action on a path that an exclusion rule matches.</summary>
    public static string ErrorMessage(string rule) => $"Path is excluded from sync by the rule '{rule}'";

    /// <summary>
    /// Returns the rule the path matches, or null when it is not excluded. A folder is tested with a trailing <c>/</c>.
    /// </summary>
    public string? Match(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').TrimStart('/');

        foreach (var rule in _rules)
        {
            if (rule.Regex.IsMatch(path))
            {
                return rule.Pattern;
            }
        }

        return null;
    }

    private static Regex? RuleToRegex(string rule)
    {
        var pattern = rule.Trim().Replace('\\', '/');
        if (pattern.Length == 0 || pattern.StartsWith('#'))
        {
            return null;
        }

        // A trailing slash only matches folders, so something has to follow it in the path
        var folderOnly = pattern.EndsWith('/');
        pattern = pattern.TrimEnd('/');

        // A rule with a slash is relative to the repository; otherwise it matches a name at any depth
        var anchored = pattern.Contains('/');
        pattern = pattern.TrimStart('/');
        if (pattern.Length == 0)
        {
            return null;
        }

        var body = new StringBuilder();
        var i = 0;
        while (i < pattern.Length)
        {
            var atSegmentStart = i == 0 || pattern[i - 1] == '/';

            if (atSegmentStart && string.CompareOrdinal(pattern, i, "**/", 0, 3) == 0)
            {
                body.Append("(?:.*/)?");
                i += 3;
            }
            else if (i + 3 == pattern.Length && string.CompareOrdinal(pattern, i, "/**", 0, 3) == 0)
            {
                body.Append("/.*");
                i += 3;
            }
            else if (pattern[i] == '*')
            {
                body.Append("[^/]*");
                i++;
            }
            else if (pattern[i] == '?')
            {
                body.Append("[^/]");
                i++;
            }
            else
            {
                body.Append(Regex.Escape(pattern[i].ToString()));
                i++;
            }
        }

        var prefix = anchored ? "^" : "^(?:.*/)?";
        var suffix = folderOnly ? "/.*\\z" : "(?:/.*)?\\z";

        return new Regex(prefix + body + suffix,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}
