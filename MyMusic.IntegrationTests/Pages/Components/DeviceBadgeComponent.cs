using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class DeviceBadgeComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task<string> GetNameAsync()
    {
        var text = await Root.InnerTextAsync();
        return text.Trim();
    }

    public async Task<string?> GetSyncActionAsync()
    {
        return await Root.GetAttributeAsync("data-sync-action");
    }

    public async Task<bool> HasSyncActionAsync(string action)
    {
        var syncAction = await GetSyncActionAsync();
        if (syncAction == null)
            return false;

        if (action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
        {
            return syncAction.Equals("Remove", StringComparison.OrdinalIgnoreCase);
        }
        return syncAction.Equals(action, StringComparison.OrdinalIgnoreCase);
    }
}
