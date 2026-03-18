using System.Linq.Expressions;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingYearAuditRule : AuditRuleBase
{
    public override long Id => 2;
    public override string Name => "Missing Year";
    public override string Icon => "IconCalendarOff";
    public override string Description => "Songs that do not have a release year.";

    protected override Expression<Func<Song, bool>> GetViolationPredicate() =>
        s => s.Year == null;

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic year detection is not yet implemented.");
}
