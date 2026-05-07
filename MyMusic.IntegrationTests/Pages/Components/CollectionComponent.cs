using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class CollectionComponent(ILocator root) : BaseComponent(root)
{
    public ILocator GetCell(string column, object rowKey) =>
        Root.GetByTestId($"collection-cell-{column}-{rowKey}");

    public async Task<int> GetRowCountAsync()
    {
        var rows = Root.Locator("tr[data-index]");
        return await rows.CountAsync();
    }

    public async Task WaitForVisibleAsync()
    {
        await Root.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    public async Task WaitForLoadedAsync(int timeout = 10000)
    {
        await WaitForVisibleAsync();
        await WaitForAttributeAsync("data-loading", "false", timeout);
    }

    public async Task GoToDetailsAsync(int rowIndex, string linkSelector = "a")
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        var firstLink = row.Locator(linkSelector).First;
        await firstLink.ClickAsync();
    }

    /// <summary>
    /// Finds a cell by column name containing the specified text and clicks the link inside it.
    /// Uses Playwright's auto-waiting and retry logic.
    /// </summary>
    public async Task GoToDetailsByCellTextAsync(string columnName, string cellText)
    {
        var cell = Root.Locator($"td[data-testid^='collection-cell-{columnName}-']")
            .Filter(new LocatorFilterOptions { HasText = cellText })
            .First;
        
        var link = cell.Locator("a");
        await link.ClickAsync();
    }
}
