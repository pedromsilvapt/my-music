namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// What to set in a device through its editor. A <see langword="null"/> value leaves that field as it is.
/// <paramref name="Icon"/> is the name of one of the icons the editor offers (e.g. <c>IconDeviceTablet</c>).
/// </summary>
public record EditDeviceOptions(
    string? Name = null,
    string? Icon = null,
    string? Color = null,
    bool? ImportOnPurchase = null,
    string? NamingTemplate = null);
