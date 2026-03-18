using System.Linq.Expressions;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingLyricsAuditRule : AuditRuleBase
{
    public override long Id => 4;
    public override string Name => "Missing Lyrics";
    public override string Icon => "IconTextOff";
    public override string Description => "Songs that do not have lyrics.";

    protected override Expression<Func<Song, bool>> GetViolationPredicate() =>
        s => !s.HasLyrics;

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic lyrics fetching is not yet implemented.");
}
