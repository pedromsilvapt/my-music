using System.Runtime.CompilerServices;
using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.AuditRules;

public class MissingFileAuditRule(IFileSystem fileSystem) : IAuditRule
{
    public long Id => 10;
    public string Name => "Missing Files";
    public string Icon => "IconFileOff";
    public string Description => "Songs whose audio files no longer exist on disk.";
    public string? CustomPage => null;

    public async IAsyncEnumerable<AuditNonConformity> Scan(
        MusicDbContext db,
        long ownerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var existingNonConformingSongIds = await db.AuditNonConformities
            .Where(nc => nc.AuditRuleId == Id && nc.OwnerId == ownerId)
            .Select(nc => nc.SongId)
            .ToListAsync(cancellationToken);

        var songs = await db.Songs
            .Where(s => s.OwnerId == ownerId)
            .Where(s => !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => new { s.Id, s.RepositoryPath })
            .ToListAsync(cancellationToken);

        foreach (var song in songs)
        {
            if (!fileSystem.File.Exists(song.RepositoryPath))
            {
                yield return new AuditNonConformity
                {
                    SongId = song.Id,
                    AuditRuleId = Id,
                    OwnerId = ownerId,
                    HasWaiver = false,
                    CreatedAt = DateTime.UtcNow,
                };
            }
        }
    }

    public Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Cannot automatically fix a missing file. Re-import the file or delete the song.");
}
