using System.Linq.Expressions;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class NonSquareCoverAuditRule : AuditRuleBase
{
    public override long Id => 8;
    public override string Name => "Non-Square Covers";
    public override string Icon => "IconAspectRatio";
    public override string Description => "Songs with cover artwork not in 1:1 aspect ratio.";

    protected override Expression<Func<Song, bool>> GetViolationPredicate() =>
        s => s.CoverId != null
             && s.Cover != null
             && s.Cover.Width != s.Cover.Height;

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic cover resizing to square is not yet implemented.");
}
