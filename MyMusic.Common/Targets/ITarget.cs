using MyMusic.Common.Metadata;

namespace MyMusic.Common.Targets;

public interface ITarget
{
    public Task Save(Stream data, SongMetadata? metadata = null, CancellationToken cancellationToken = default);

    public Stream Read();

    public Task SaveMetadata(SongMetadata metadata, CancellationToken cancellationToken = default);

    public Task<SongMetadata> ReadMetadata(CancellationToken cancellationToken = default);

    public Task SetTimestamps(DateTime createdAt, DateTime modifiedAt, CancellationToken cancellationToken = default);

    public Task Relocate(CancellationToken cancellationToken = default);
}