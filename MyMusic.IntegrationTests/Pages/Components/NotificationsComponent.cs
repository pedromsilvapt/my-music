using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

/// <summary>
/// The notification toasts shown on top of the page.
/// </summary>
public class NotificationsComponent(ILocator locator) : BaseComponent(locator)
{
    /// <summary>
    /// Waits for a notification containing <paramref name="message"/> to show up.
    /// </summary>
    public async Task WaitForMessageAsync(string message)
    {
        var notification = Root.GetByRole(AriaRole.Alert).Filter(new() { HasText = message });
        await Assertions.Expect(notification).ToBeVisibleAsync();
    }
}
