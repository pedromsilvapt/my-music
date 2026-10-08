using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Updates an existing <see cref="Device"/> owned by the current user. Reuses
/// <see cref="IDeviceLookupService"/> for the device lookup to keep device identity operations
/// centralized.
/// </summary>
public interface IDeviceUpdateService
{
    /// <summary>
    /// Updates the editable fields of a device owned by the current user. Returns <c>null</c>
    /// when no such device exists (mirrors the previous controller <c>NotFound</c> path).
    /// </summary>
    /// <exception cref="DeviceNameAlreadyExistsException">The user has another device with that name.</exception>
    /// <exception cref="ValidationException">The name or the naming template is not valid.</exception>
    Task<DeviceUpdateResult?> UpdateAsync(
        long deviceId,
        DeviceUpdateInput input,
        CancellationToken cancellationToken);
}

/// <summary>
/// Input for a device update operation. Mirrors <c>UpdateDeviceRequest</c> but lives in
/// <see cref="MyMusic.Common"/> so the service has no dependency on the Server DTO layer.
/// </summary>
public record DeviceUpdateInput
{
    /// <summary>The new name of the device; <c>null</c> keeps the current one.</summary>
    public string? Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
    public bool? ImportOnPurchase { get; init; }
}

/// <summary>
/// Result of a device update operation. The controller maps this to <c>UpdateDeviceResponse</c>.
/// </summary>
public record DeviceUpdateResult
{
    public required Device Device { get; init; }
}