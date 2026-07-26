namespace MyMusic.Common.Services.Devices;

/// <summary>
/// Returns the distinct values for a single device filter field, scoped to the current
/// owner. Extracted from DevicesController.GetFilterValues so the controller stays thin
/// (input/output + DTO mapping only). The filter metadata endpoint (pure static output)
/// remains in the controller.
/// </summary>
public interface IDeviceFilterValuesService
{
    /// <summary>
    /// Returns the distinct, ordered, optionally searched, and limited values for the
    /// given <paramref name="field"/> across the devices owned by <paramref name="ownerId"/>.
    /// Unknown fields return an empty list (mirrors the prior controller behavior).
    /// </summary>
    Task<DeviceFilterValuesResult> GetAsync(
        long ownerId,
        string field,
        string? search,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a device filter-values operation. The controller maps this to
/// <c>FilterValuesResponse</c>.
/// </summary>
public record DeviceFilterValuesResult
{
    public required List<string> Values { get; init; }
}