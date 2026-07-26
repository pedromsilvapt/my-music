using MyMusic.Common.Entities;

namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Creates a new <see cref="Device"/> scoped to the current user. Extracted from
/// DevicesController.Create so the controller stays thin (input/output + DTO mapping only).
/// </summary>
public interface IDeviceCreateService
{
    /// <summary>
    /// Creates a device owned by the current user. Returns <c>null</c> when the current user
    /// does not exist (mirrors the previous controller <c>NotFound</c> path).
    /// </summary>
    Task<DeviceCreateResult?> CreateAsync(
        DeviceCreateInput input,
        CancellationToken cancellationToken);
}

/// <summary>
/// Input for a device create operation. Mirrors <c>CreateDeviceRequest</c> but lives in
/// <see cref="MyMusic.Common"/> so the service has no dependency on the Server DTO layer.
/// </summary>
public record DeviceCreateInput
{
    public required string Name { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
    public string? NamingTemplate { get; init; }
    public bool ImportOnPurchase { get; init; }
}

/// <summary>
/// Result of a device create operation. The controller maps this to <c>CreateDeviceResponse</c>.
/// </summary>
public record DeviceCreateResult
{
    public required Device Device { get; init; }
}