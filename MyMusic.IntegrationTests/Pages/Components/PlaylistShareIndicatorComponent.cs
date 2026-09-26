using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The icon marking a shared playlist: <c>shared-by-me</c> for the owner, <c>shared-with-me</c> for recipients.
/// </summary>
public class PlaylistShareIndicatorComponent(ILocator locator) : BaseComponent(locator)
{
    public const string SharedByMe = "shared-by-me";
    public const string SharedWithMe = "shared-with-me";

    /// <summary>
    /// Returns the indicator's share state, or <see langword="null"/> when the playlist is not marked as shared.
    /// </summary>
    public async Task<string?> GetStateAsync()
    {
        if (await Root.CountAsync() == 0)
            return null;

        return await Root.GetAttributeAsync("data-share-state");
    }
}
