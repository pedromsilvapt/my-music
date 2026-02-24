namespace MyMusic.Common.Services;

public interface IAuditRule
{
    long Id { get; }
    string Name { get; }
    string Icon { get; }
    string Description { get; }
    Task<int> Scan(MusicDbContext db, long ownerId, CancellationToken cancellationToken = default);
    Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default);
}