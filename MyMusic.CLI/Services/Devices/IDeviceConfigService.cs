using MyMusic.CLI.Api.Dtos;

namespace MyMusic.CLI.Services.Devices;

/// <summary>
/// Resolves the server device matching the configured device name. Devices, and their options (icon,
/// color, naming template, import on purchase), are created and edited in the web app.
/// </summary>
public interface IDeviceConfigService
{
    /// <summary>
    /// Finds the server device that has the configured name.
    /// </summary>
    /// <exception cref="DeviceNotFoundException">The server has no device with that name.</exception>
    Task<ListDeviceItem> ResolveAsync(CancellationToken ct = default);
}

/// <summary>
/// Thrown when the server has no device with the configured name.
/// </summary>
public class DeviceNotFoundException(string deviceName)
    : Exception($"Device '{deviceName}' not found. Create it in the web app (Devices > New device)");
