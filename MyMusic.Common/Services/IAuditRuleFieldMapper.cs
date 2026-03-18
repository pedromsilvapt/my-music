namespace MyMusic.Common.Services;

/// <summary>
/// Maps audit rule IDs to metadata fields for pre-selection in edit modal.
/// </summary>
public interface IAuditRuleFieldMapper
{
    /// <summary>
    /// Gets the list of metadata fields associated with an audit rule.
    /// </summary>
    /// <param name="ruleId">The audit rule ID (1-8)</param>
    /// <returns>List of field names (e.g., "cover", "year", "genres", etc.)</returns>
    List<string> GetFieldsForRule(long ruleId);

    /// <summary>
    /// Gets all unique fields for a collection of audit rules.
    /// </summary>
    /// <param name="ruleIds">Collection of rule IDs</param>
    /// <returns>List of unique field names</returns>
    List<string> GetFieldsForRules(IEnumerable<long> ruleIds);
}
