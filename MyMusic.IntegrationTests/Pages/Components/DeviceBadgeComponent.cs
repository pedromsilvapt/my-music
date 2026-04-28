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
        var tooltip = await Root.GetAttributeAsync("aria-label");
        if (string.IsNullOrEmpty(tooltip))
        {
            return null;
        }

        if (tooltip.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            tooltip.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        {
            return "Remove";
        }

        if (tooltip.Contains("Download", StringComparison.OrdinalIgnoreCase))
        {
            return "Download";
        }

        return null;
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
