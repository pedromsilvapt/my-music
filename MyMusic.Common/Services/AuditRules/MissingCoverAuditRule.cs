using System.Linq.Expressions;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingCoverAuditRule : AuditRuleBase
{
    public override long Id => 1;
    public override string Name => "Missing Cover";
    public override string Icon => "IconPhotoOff";
    public override string Description => "Songs that do not have an associated cover image.";

    protected override Expression<Func<Song, bool>> GetViolationPredicate() =>
        s => s.CoverId == null;

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic cover extraction is not yet implemented.");
}
