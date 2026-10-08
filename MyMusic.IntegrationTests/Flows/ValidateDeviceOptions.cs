namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// What a device's details page should show. A <see langword="null"/> value is not checked.
/// </summary>
public record ValidateDeviceOptions(
    string? Icon = null,
    string? Color = null,
    bool? ImportOnPurchase = null,
    string? NamingTemplate = null,
    bool? UsesDefaultNamingTemplate = null,
    int? SongCount = null,
    int? SessionsCount = null);
