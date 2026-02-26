using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.Common.Entities;

namespace MyMusic.Common.Seeding;

public class SeedService(
    IFileSystem fileSystem,
    MusicDbContext db,
    IOptions<Config> config,
    ILogger<SeedService> logger) : ISeedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedPath = config.Value.SeedPath;
        if (string.IsNullOrEmpty(seedPath))
            return;

        if (!fileSystem.File.Exists(seedPath))
        {
            logger.LogWarning("Seed file not found at {SeedPath}", seedPath);
            return;
        }

        logger.LogInformation("Seeding data from {SeedPath}", seedPath);

        await using var stream = fileSystem.File.Open(seedPath, FileMode.Open, FileAccess.Read);
        var seedData = await JsonSerializer.DeserializeAsync<SeedData>(stream, JsonOptions, cancellationToken);

        if (seedData is null)
            return;

        await SeedUsersAsync(seedData.Users, cancellationToken);
        await SeedSourcesAsync(seedData.Sources, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUsersAsync(List<SeedUser>? users, CancellationToken cancellationToken)
    {
        if (users is null || users.Count == 0)
            return;

        foreach (var seedUser in users)
        {
            var user = await UpsertAsync(
                db.Users,
                seedUser.Id,
                u => u.Username == seedUser.Username,
                () => new User
                {
                    Username = seedUser.Username,
                    Name = seedUser.Name,
                },
                existing =>
                {
                    existing.Name = seedUser.Name;
                    existing.Username = seedUser.Username;
                },
                cancellationToken);

            if (user.Id == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            await SeedDevicesAsync(user.Id, seedUser.Devices, cancellationToken);
        }
    }

    private async Task SeedDevicesAsync(long ownerId, List<SeedDevice>? devices, CancellationToken cancellationToken)
    {
        if (devices is null || devices.Count == 0)
            return;

        foreach (var seedDevice in devices)
        {
            await UpsertAsync(
                db.Devices,
                seedDevice.Id,
                d => d.OwnerId == ownerId && d.Name == seedDevice.Name,
                () => new Device
                {
                    Name = seedDevice.Name,
                    OwnerId = ownerId,
                    Owner = null!,
                    Icon = seedDevice.Icon,
                    Color = seedDevice.Color,
                    NamingTemplate = seedDevice.NamingTemplate,
                },
                existing =>
                {
                    existing.Name = seedDevice.Name;
                    existing.Icon = seedDevice.Icon;
                    existing.Color = seedDevice.Color;
                    existing.NamingTemplate = seedDevice.NamingTemplate;
                },
                cancellationToken);
        }
    }

    private async Task SeedSourcesAsync(List<SeedSource>? sources, CancellationToken cancellationToken)
    {
        if (sources is null || sources.Count == 0)
            return;

        foreach (var seedSource in sources)
        {
            await UpsertAsync(
                db.Sources,
                seedSource.Id,
                s => s.Name == seedSource.Name,
                () => new Source
                {
                    Name = seedSource.Name,
                    Icon = seedSource.Icon,
                    Address = seedSource.Address,
                    IsPaid = seedSource.IsPaid,
                },
                existing =>
                {
                    existing.Name = seedSource.Name;
                    existing.Icon = seedSource.Icon;
                    existing.Address = seedSource.Address;
                    existing.IsPaid = seedSource.IsPaid;
                },
                cancellationToken);
        }
    }

    private async Task<TEntity> UpsertAsync<TEntity>(
        DbSet<TEntity> dbSet,
        long? id,
        System.Linq.Expressions.Expression<Func<TEntity, bool>> uniqueKeyPredicate,
        Func<TEntity> createEntity,
        Action<TEntity> updateEntity,
        CancellationToken cancellationToken) where TEntity : class
    {
        TEntity? entity = null;

        if (id.HasValue)
        {
            entity = await dbSet.FindAsync([id.Value], cancellationToken);
        }

        if (entity is null)
        {
            entity = await dbSet.FirstOrDefaultAsync(uniqueKeyPredicate, cancellationToken);
        }

        if (entity is null)
        {
            entity = createEntity();
            await dbSet.AddAsync(entity, cancellationToken);
        }
        else
        {
            updateEntity(entity);
        }

        return entity;
    }
}