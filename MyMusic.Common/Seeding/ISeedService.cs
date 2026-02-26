namespace MyMusic.Common.Seeding;

public interface ISeedService
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}