using Microsoft.EntityFrameworkCore;

namespace MyMusic.Common.Tests.Utilities;

/// <summary>
///     Creates contexts sharing the same options (and so the same in-memory SQLite connection and interceptors), the
///     way the production factory creates contexts on the same database.
/// </summary>
public class TestDbContextFactory(DbContextOptions options) : IDbContextFactory<MusicDbContext>
{
    public MusicDbContext CreateDbContext() => new(options);
}
