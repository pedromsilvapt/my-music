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

    /// <summary>
    /// Gets the inner text of a cell in a specific row and column.
    /// </summary>
    public async Task<string> GetCellTextAsync(int rowIndex, string columnName)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        var cell = row.Locator($"td[data-testid^='collection-cell-{columnName}-']");
        return await cell.InnerTextAsync();
    }

    /// <summary>
    /// Finds the first row index where the specified column cell contains the given text.
    /// Returns -1 if not found.
    /// </summary>
    public async Task<int> FindRowByCellTextAsync(string columnName, string cellText)
    {
        var rowCount = await GetRowCountAsync();
        for (int i = 0; i < rowCount; i++)
        {
            var text = await GetCellTextAsync(i, columnName);
            if (text == cellText)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Clicks on a row at the given index to select it.
    /// </summary>
    public async Task SelectRowByIndexAsync(int rowIndex)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        await row.ClickAsync();
    }

    /// <summary>
    /// Ctrl-clicks on a row at the given index to add it to the current selection.
    /// </summary>
    public async Task CtrlClickRowByIndexAsync(int rowIndex)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        await row.ScrollIntoViewIfNeededAsync();
        var box = await row.BoundingBoxAsync();
        if (box == null)
            throw new InvalidOperationException($"Row {rowIndex} not found");

        await Root.Page.Keyboard.DownAsync("Control");
        await Root.Page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Root.Page.Keyboard.UpAsync("Control");
    }

    /// <summary>
    /// Right-clicks on a row at the given index to open the context menu.
    /// </summary>
    public async Task RightClickRowByIndexAsync(int rowIndex)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        await row.ClickAsync(new() { Button = MouseButton.Right });
    }

    /// <summary>
    /// Right-clicks on a row containing the specified text in the given column.
    /// </summary>
    public async Task RightClickRowByCellTextAsync(string columnName, string cellText)
    {
        var cell = Root.Locator($"td[data-testid^='collection-cell-{columnName}-']")
            .Filter(new LocatorFilterOptions { HasText = cellText })
            .First;

        var row = cell.Locator("ancestor::tr").First;
        await row.ClickAsync(new() { Button = MouseButton.Right });
    }

    /// <summary>
    /// Checks if any cell in the collection contains the specified text.
    /// </summary>
    public async Task<bool> HasItemByTextAsync(string text)
    {
        var cell = Root.Locator("[data-testid^='collection-cell-']").Filter(new() { HasText = text });
        return await cell.CountAsync() > 0;
    }

    /// <summary>
    /// Returns a locator for the floating selection actions bar.
    /// </summary>
    public ILocator GetFloatingActionsBar() =>
        Root.Page.Locator(".mantine-Paper-root").Filter(new()
        {
            Has = Root.Page.GetByText(" selected"),
        });

    /// <summary>
    /// Clicks the Actions menu button in the floating bar and returns the menu dropdown locator.
    /// </summary>
    public async Task<ILocator> OpenFloatingActionsMenuAsync()
    {
        var floatingBar = GetFloatingActionsBar();
        await floatingBar.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var actionsButton = floatingBar.GetByRole(AriaRole.Button, new() { Name = "Actions" });
        await actionsButton.ClickAsync();

        // The Mantine Menu dropdown is a sibling of the button, rendered in a portal
        var menuDropdown = Root.Page.Locator(".mantine-Menu-dropdown").Last;
        await menuDropdown.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return menuDropdown;
    }
}
