using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageSongItemComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task<string> GetTitleAsync()
    {
        var titleElement = Root.Locator("span[fw='500']").First;
        return await titleElement.TextContentAsync() ?? string.Empty;
    }

    public async Task<string?> GetPathAsync()
    {
        var pathElement = Root.Locator("[data-testid='song-path']");
        var count = await pathElement.CountAsync();
        if (count == 0)
        {
            return null;
        }
        return await pathElement.TextContentAsync();
    }

    public async Task<string?> GetSyncActionAsync()
    {
        var actionElement = Root.Locator("[data-testid='sync-action']");
        var count = await actionElement.CountAsync();
        if (count == 0)
        {
            return null;
        }
        return await actionElement.GetAttributeAsync("data-action");
    }

    public async Task<bool> IsIncludedAsync()
    {
        var included = await Root.GetAttributeAsync("data-included");
        return included == "true";
    }
}
