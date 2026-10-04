using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMusic.CLI.Api;
using MyMusic.CLI.Api.Dtos;
using MyMusic.CLI.Configuration;

namespace MyMusic.CLI.Services.Devices;

/// <summary>
/// Default implementation of <see cref="IDeviceConfigService"/>.
/// </summary>
public class DeviceConfigService(
    IOptions<MyMusicOptions> options,
    IMyMusicClient client,
    ILogger<DeviceConfigService> logger) : IDeviceConfigService
{
    /// <inheritdoc />
    public async Task<DeviceConfigResult> ResolveAsync(bool saveOptions, CancellationToken ct = default)
    {
        var device = options.Value.Device;

        var devicesResponse = await client.GetDevicesAsync(ct);
        var existingDevice = devicesResponse.Devices.FirstOrDefault(d => d.Name == device.Name);

        if (existingDevice is null)
        {
            logger.LogInformation("Creating new device: {DeviceName}", device.Name);
            var newDevice = await client.CreateDeviceAsync(new CreateDeviceRequest
            {
                Name = device.Name,
                Icon = device.Icon,
                Color = device.Color,
                NamingTemplate = device.NamingTemplate,
                ImportOnPurchase = device.ImportOnPurchase,
            }, ct);
            logger.LogInformation("Created device with ID: {DeviceId}", newDevice.Device.Id);
            return new DeviceConfigResult { DeviceId = newDevice.Device.Id, Outcome = DeviceConfigOutcome.Created };
        }

        logger.LogInformation("Found existing device: {DeviceName} (ID: {DeviceId})",
            existingDevice.Name, existingDevice.Id);

        var needsUpdate = existingDevice.Icon != device.Icon ||
                          existingDevice.Color != device.Color ||
                          existingDevice.NamingTemplate != device.NamingTemplate ||
                          existingDevice.ImportOnPurchase != device.ImportOnPurchase;

        if (!saveOptions || !needsUpdate)
        {
            return new DeviceConfigResult { DeviceId = existingDevice.Id, Outcome = DeviceConfigOutcome.Unchanged };
        }

        logger.LogInformation("Updating device properties for: {DeviceName}", existingDevice.Name);
        await client.UpdateDeviceAsync(existingDevice.Id, new UpdateDeviceRequest
        {
            Icon = device.Icon,
            Color = device.Color,
            NamingTemplate = device.NamingTemplate,
            ImportOnPurchase = device.ImportOnPurchase,
        }, ct);

        return new DeviceConfigResult { DeviceId = existingDevice.Id, Outcome = DeviceConfigOutcome.Updated };
    }
}
