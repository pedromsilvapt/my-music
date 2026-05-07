using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyMusic.Common;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MusicDbContext>
{
    public MusicDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MusicDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=mymusic;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .UseProjectables();

        return new MusicDbContext(optionsBuilder.Options);
    }
}