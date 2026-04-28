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
}
