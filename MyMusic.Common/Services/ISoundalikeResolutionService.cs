using MyMusic.Common.Services.AuditRules;

namespace MyMusic.Common.Services;

public interface ISoundalikeResolutionService
{
    Task<int> ResolveAsync(MusicDbContext db, long ownerId, List<GroupResolutionInput> resolutions, CancellationToken cancellationToken = default);
}

public record GroupResolutionInput
{
    public required long NonConformityId { get; init; }
    public required long PrimarySongId { get; init; }
    public required List<SecondarySongActionInput> SecondaryActions { get; init; }
}

public record SecondarySongActionInput
{
    public required long SongId { get; init; }
    public required SecondaryAction Action { get; init; }
}
