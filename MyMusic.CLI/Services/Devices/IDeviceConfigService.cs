namespace MyMusic.CLI.Services.Devices;

/// <summary>
/// Resolves the server device matching the configured device name, registering it when it doesn't
/// exist yet.
/// </summary>
public interface IDeviceConfigService
{
    /// <summary>
    /// Finds the configured device on the server, creating it when missing, and returns its ID.
    /// </summary>
    /// <param name="saveOptions">
    /// When set, the configured options (icon, color, naming template, import on purchase) are saved
    /// to an existing device that differs from them. A device that has to be created always gets them.
    /// </param>
    Task<DeviceConfigResult> ResolveAsync(bool saveOptions, CancellationToken ct = default);
}

public record DeviceConfigResult
{
    public required long DeviceId { get; init; }
    public required DeviceConfigOutcome Outcome { get; init; }
}

public enum DeviceConfigOutcome
{
    /// <summary>The device exists and was left as is.</summary>
    Unchanged,
    Created,
    Updated,
}
