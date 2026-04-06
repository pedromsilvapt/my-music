using System.Runtime.CompilerServices;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services;

public interface IAuditRule
{
    long Id { get; }
    string Name { get; }
    string Icon { get; }
    string Description { get; }
    string? CustomPageRoute { get; }
    IAsyncEnumerable<AuditNonConformity> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default);
    Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default);
}