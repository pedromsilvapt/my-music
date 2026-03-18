using System.Linq.Expressions;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class NonJpegCoverAuditRule : AuditRuleBase
{
    public override long Id => 7;
    public override string Name => "Non-JPEG Covers";
    public override string Icon => "IconFileType";
    public override string Description => "Songs with cover artwork in a format other than JPEG.";

    protected override Expression<Func<Song, bool>> GetViolationPredicate() =>
        s => s.CoverId != null
             && s.Cover != null
             && s.Cover.MimeType != "image/jpeg";

    public override Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Automatic cover conversion to JPEG is not yet implemented.");
}
