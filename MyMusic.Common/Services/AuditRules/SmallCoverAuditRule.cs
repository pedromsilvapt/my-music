using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class SmallCoverAuditRule(IOptions<AuditConfig> config) : ConfigurableAuditRuleBase(config)
{
    public override long Id => 6;
    public override string Name => "Small Sized Covers";
    public override string Icon => "IconPhotoMinus";

    public override string Description =>
        $"Songs with cover artwork smaller than {Config.Value.SmallCoverThreshold} pixels (on both dimensions).";

    protected override Expression<Func<Song, bool>> GetViolationPredicate()
    {
        var threshold = Config.Value.SmallCoverThreshold;

        return s => s.CoverId != null
                    && s.Cover != null
                    && (s.Cover.Width < threshold || s.Cover.Height < threshold);
    }

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic cover upscaling is not yet implemented.");
}
