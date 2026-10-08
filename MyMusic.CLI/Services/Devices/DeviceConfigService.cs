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
    public async Task<ListDeviceItem> ResolveAsync(CancellationToken ct = default)
    {
        var deviceName = options.Value.Device.Name;

        var devicesResponse = await client.GetDevicesAsync(ct);
        var device = devicesResponse.Devices.FirstOrDefault(d => d.Name == deviceName);

        if (device is null)
        {
            logger.LogError("Device not found: {DeviceName}", deviceName);
            throw new DeviceNotFoundException(deviceName);
        }

        logger.LogInformation("Found device: {DeviceName} (ID: {DeviceId})", device.Name, device.Id);

        return device;
    }
}
