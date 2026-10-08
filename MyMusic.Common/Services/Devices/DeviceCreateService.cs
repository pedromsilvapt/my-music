using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceCreateService"/>.
/// </summary>
public class DeviceCreateService(
    MusicDbContext db,
    ICurrentUser currentUser,
    ILogger<DeviceCreateService> logger) : IDeviceCreateService
{
    /// <inheritdoc />
    public async Task<DeviceCreateResult?> CreateAsync(
        DeviceCreateInput input,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.Id;
        var user = await db.Users.FindAsync([ownerId], cancellationToken);
        if (user == null) return null;

        var name = DeviceValidator.NormalizeName(input.Name);
        var namingTemplate = DeviceValidator.NormalizeNamingTemplate(input.NamingTemplate);
        await DeviceValidator.EnsureNameIsFreeAsync(db, ownerId, name, cancellationToken);

        var device = new Device
        {
            Name = name,
            Icon = input.Icon,
            Color = input.Color,
            NamingTemplate = namingTemplate,
            ImportOnPurchase = input.ImportOnPurchase,
            OwnerId = ownerId,
            Owner = user,
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created device {DeviceName} with ID {DeviceId} for user {UserId}, Template={NamingTemplate}",
            device.Name, device.Id, ownerId, device.NamingTemplate ?? "(null)");

        return new DeviceCreateResult { Device = device };
    }
}