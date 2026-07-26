using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Updates an existing <see cref="Device"/> owned by the current user. Extracted from
/// DevicesController.Update so the controller stays thin. Reuses <see cref="IDeviceLookupService"/>
/// for the device lookup to keep device identity operations centralized.
/// </summary>
public interface IDeviceUpdateService
{
    /// <summary>
    /// Updates the editable fields of a device owned by the current user. Returns <c>null</c>
    /// when no such device exists (mirrors the previous controller <c>NotFound</c> path).
    /// </summary>
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