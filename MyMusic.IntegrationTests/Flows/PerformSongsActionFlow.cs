using Microsoft.Playwright;
using MyMusic.IntegrationTests.Pages.Components;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Selects multiple songs and opens the floating actions menu.
/// Returns the open menu wrapped in a <see cref="SongsActionsMenuComponent"/> for further actions.
/// </summary>
public class PerformSongsActionFlow(params string[] songTitles) : IFlow<SongsActionsMenuComponent>
{
    public async Task<SongsActionsMenuComponent> ExecuteAsync(IPage page)
    {
        var collection = await new SelectSongsFlow(songTitles).ExecuteAsync(page);
        return await collection.OpenFloatingActionsMenuAsync();
    }
}
