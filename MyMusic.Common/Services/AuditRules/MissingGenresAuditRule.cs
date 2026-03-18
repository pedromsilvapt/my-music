using System.Linq.Expressions;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingGenresAuditRule : AuditRuleBase
{
    public override long Id => 3;
    public override string Name => "Missing Genres";
    public override string Icon => "IconTagOff";
    public override string Description => "Songs that do not have any genres assigned.";

    protected override Expression<Func<Song, bool>> GetViolationPredicate() =>
        s => s.Genres.Count == 0;

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic genre detection is not yet implemented.");
}
