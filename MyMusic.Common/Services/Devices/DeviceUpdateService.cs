using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceUpdateService"/>.
/// </summary>
public class DeviceUpdateService(
    MusicDbContext db,
    IDeviceLookupService deviceLookup,
    ICurrentUser currentUser,
    ILogger<DeviceUpdateService> logger) : IDeviceUpdateService
{
    /// <inheritdoc />
    public async Task<DeviceUpdateResult?> UpdateAsync(
        long deviceId,
        DeviceUpdateInput input,
        CancellationToken cancellationToken)
    {
        var device = await deviceLookup.FindDeviceAsync(db, deviceId, currentUser.Id, cancellationToken);
        if (device == null) return null;

        device.Icon = input.Icon;
        device.Color = input.Color;
        device.NamingTemplate = input.NamingTemplate;
        if (input.ImportOnPurchase.HasValue)
        {
            device.ImportOnPurchase = input.ImportOnPurchase.Value;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated device {DeviceId} for user {UserId}", deviceId, currentUser.Id);

        return new DeviceUpdateResult { Device = device };
    }
}