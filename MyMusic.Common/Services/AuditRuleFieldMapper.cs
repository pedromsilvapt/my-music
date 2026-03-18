namespace MyMusic.Common.Services;

/// <summary>
/// Implementation of audit rule to metadata field mapping.
/// </summary>
public class AuditRuleFieldMapper : IAuditRuleFieldMapper
{
    // Rule ID mapping based on data-model.md specification:
    // 1: MissingCover -> cover
    // 2: MissingYear -> year
    // 3: MissingGenres -> genres
    // 4: MissingLyrics -> lyrics
    // 5: MediumCover -> cover
    // 6: SmallCover -> cover
    // 7: NonJpegCover -> cover
    // 8: NonSquareCover -> cover

    private static readonly Dictionary<long, List<string>> RuleFieldMap = new()
    {
        { 1, ["cover"] },           // MissingCover
        { 2, ["year"] },            // MissingYear
        { 3, ["genres"] },          // MissingGenres
        { 4, ["lyrics"] },          // MissingLyrics
        { 5, ["cover"] },           // MediumCover
        { 6, ["cover"] },           // SmallCover
        { 7, ["cover"] },           // NonJpegCover
        { 8, ["cover"] }            // NonSquareCover
    };

    public List<string> GetFieldsForRule(long ruleId)
    {
        if (RuleFieldMap.TryGetValue(ruleId, out var fields))
        {
            return fields.ToList();
        }

        // Return empty list for unknown rules
        return [];
    }

    public List<string> GetFieldsForRules(IEnumerable<long> ruleIds)
    {
        var fields = new HashSet<string>();

        foreach (var ruleId in ruleIds)
        {
            var ruleFields = GetFieldsForRule(ruleId);
            foreach (var field in ruleFields)
            {
                fields.Add(field);
            }
        }

        return fields.ToList();
    }
}
