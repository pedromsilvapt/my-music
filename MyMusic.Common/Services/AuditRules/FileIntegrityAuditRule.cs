using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.AudioIntegrity;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;

namespace MyMusic.Common.Services.AuditRules;

public class FileIntegrityAuditRule(
    IAudioIntegrityService integrityService,
    IOptions<AudioIntegrityConfig> config,
    ILogger<FileIntegrityAuditRule> logger) : IAuditRule
{
    public long Id => 11;
    public string Name => "File Integrity";
    public string Icon => "IconFileAlert";
    public string Description => "Songs with corrupted, truncated, or structurally invalid audio files.";
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
            .Where(s => s.RepositoryPath != null)
            .Where(s => !existingNonConformingSongIds.Contains(s.Id))
            .Select(s => new { s.Id, s.RepositoryPath })
            .ToListAsync(cancellationToken);

        foreach (var song in songs)
        {
            var report = await integrityService.ValidateAsync(song.RepositoryPath!, cancellationToken);
            if (report.Status is AudioIntegrityStatus.Corrupted or AudioIntegrityStatus.Truncated)
            {
                yield return new AuditNonConformity
                {
                    SongId = song.Id,
                    AuditRuleId = Id,
                    OwnerId = ownerId,
                    HasWaiver = false,
                    CreatedAt = DateTime.UtcNow,
                    Data = JsonSerializer.SerializeToElement(report),
                };
            }
        }
    }

    public Task Patch(MusicDbContext db, long songId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Cannot automatically fix a corrupted audio file. Re-import the file or delete the song.");
}
