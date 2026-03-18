using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

/// <summary>
/// Base class for audit rules that require configuration (IOptions&lt;AuditConfig&gt;).
/// Provides the same scanning logic as AuditRuleBase but with access to configuration values.
/// </summary>
public abstract class ConfigurableAuditRuleBase(IOptions<AuditConfig> config) : AuditRuleBase
{
    protected IOptions<AuditConfig> Config { get; } = config;
}
