using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Flows;

/// <summary>
/// Selects multiple songs and opens the floating actions menu.
/// Returns the menu dropdown locator for further actions.
/// </summary>
public class PerformSongsActionFlow(params string[] songTitles) : IFlow<ILocator>
{
    public async Task<ILocator> ExecuteAsync(IPage page)
    {
        var collection = await new SelectSongsFlow(songTitles).ExecuteAsync(page);
        return await collection.OpenFloatingActionsMenuAsync();
    }
}
