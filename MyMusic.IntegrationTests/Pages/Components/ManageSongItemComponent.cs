using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class ManageSongItemComponent(ILocator locator) : BaseComponent(locator)
{
    public async Task<string> GetTitleAsync()
    {
        return await Root.GetByTestId("song-title").TextContentAsync() ?? string.Empty;
    }

    private ILocator PathInput => Root.GetByTestId("song-path-input");

    /// <summary>
    /// Returns the path shown for the song: the value of the path input when the path can be edited,
    /// or the path text otherwise.
    /// </summary>
    public async Task<string?> GetPathAsync()
    {
        if (await PathInput.CountAsync() > 0)
        {
            return await PathInput.InputValueAsync();
        }

        var pathElement = Root.Locator("[data-testid='song-path']");
        var count = await pathElement.CountAsync();
        if (count == 0)
        {
            return null;
        }
        return await pathElement.TextContentAsync();
    }

    public async Task SetPathAsync(string path)
    {
        await PathInput.FillAsync(path);
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
