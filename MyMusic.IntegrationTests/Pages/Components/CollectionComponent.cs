using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MyMusic.IntegrationTests.Pages.Components;

public class CollectionComponent(ILocator root) : BaseComponent(root)
{
    public ILocator GetCell(string column, object rowKey) =>
        Root.GetByTestId($"collection-cell-{column}-{rowKey}");

    public ILocator Rows => Root.Locator("tr[data-index]");

    public ILocator SearchInput => Root.GetByTestId("collection-search");

    /// <summary>
    /// Types into the toolbar's search box. The search is applied after a short debounce, so assert on
    /// the resulting rows with auto-retrying expectations.
    /// </summary>
    public async Task SearchAsync(string text)
    {
        await SearchInput.FillAsync(text);
    }

    public ILocator ColumnHeaders => Root.GetByRole(AriaRole.Columnheader);

    private ILocator TableScrollArea => Root.GetByTestId("collection-table-scroll");

    /// <summary>
    /// Rendered width, in pixels, of each column header of the table view, in display order.
    /// </summary>
    public async Task<double[]> GetColumnHeaderWidthsAsync()
    {
        return await ColumnHeaders.EvaluateAllAsync<double[]>(
            "headers => headers.map(header => header.getBoundingClientRect().width)");
    }

    /// <summary>
    /// Scrolls the table view all the way down, waiting for the last of <paramref name="totalRows"/> rows
    /// to be rendered by the virtualizer.
    /// </summary>
    public async Task ScrollTableToEndAsync(int totalRows)
    {
        await TableScrollArea.EvaluateAsync("area => area.scrollTo(0, area.scrollHeight)");
        await Assertions.Expect(Root.Locator($"tr[data-index=\"{totalRows - 1}\"]")).ToBeVisibleAsync();
    }

    public async Task<int> GetRowCountAsync()
    {
        return await Rows.CountAsync();
    }

    /// <summary>
    /// Opens the "Go to item" modal from the collection toolbar.
    /// </summary>
    public async Task<GoToModalComponent> OpenGoToAsync()
    {
        await Root.GetByTestId("collection-goto").ClickAsync();

        // The modal is rendered in a portal, outside the collection root
        var modal = new GoToModalComponent(Root.Page.GetByRole(AriaRole.Dialog, new() { Name = "Go to item" }));
        await modal.WaitForVisibleAsync();
        return modal;
    }

    /// <summary>
    /// Rows (or grid/list items) currently flashing after the collection scrolled to them.
    /// </summary>
    public ILocator HighlightedRows => Root.Locator("[data-highlighted='true']");

    /// <summary>
    /// Jumps to an item through the "Go to item" modal, either clicking its option or pressing Enter
    /// on the (auto-selected) first match.
    /// </summary>
    public async Task GoToAsync(string text, bool useKeyboard = false)
    {
        var goTo = await OpenGoToAsync();
        await goTo.SearchAsync(text);
        if (useKeyboard)
            await goTo.SubmitWithEnterAsync();
        else
            await goTo.SelectAsync(text);
    }

    /// <summary>
    /// Waits for the current scroll request (e.g. from <see cref="GoToAsync"/>) to run its whole course:
    /// scroll to the item, flash it, and clear. Unlike asserting on the flash itself, which only lasts a
    /// few seconds, this can't be missed when the browser or the test runner is slow.
    /// </summary>
    public async Task WaitForScrollRequestFinishedAsync()
    {
        await WaitForAttributeAsync("data-scroll-request", "idle");
    }

    /// <summary>
    /// Adds a sort field through the toolbar's "Customize" popover. Unlike the column headers, this
    /// doesn't scroll the collection, since the toolbar stays in view.
    /// </summary>
    public async Task SortByAsync(string fieldName, bool descending = false)
    {
        var page = Root.Page;
        await Root.GetByRole(AriaRole.Button, new() { Name = "Customize" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Add sort field" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = fieldName, Exact = true }).ClickAsync();

        // New sort fields start ascending; the direction button flips them
        if (descending)
            await page.GetByTitle("Ascending", new() { Exact = true }).ClickAsync();

        await page.Keyboard.PressAsync("Escape");
    }

    /// <summary>
    /// Asserts that the collection doesn't scroll <paramref name="item"/> into view on its own,
    /// giving any (unwanted) smooth scroll enough time to start and finish.
    /// </summary>
    public async Task ShouldNotScrollToAsync(ILocator item)
    {
        await Root.Page.WaitForTimeoutAsync(1500);
        await Assertions.Expect(item).Not.ToBeInViewportAsync(new() { Timeout = 0 });
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
        // Click the duration cell (plain text) to avoid hitting anchors/svgs
        // inside other columns that suppress row selection and trigger navigation.
        var safeCell = row.Locator("td[data-testid^='collection-cell-duration-']");
        await safeCell.ClickAsync();
    }

    /// <summary>
    /// Ctrl-clicks on a row at the given index to add it to the current selection.
    /// </summary>
    public async Task CtrlClickRowByIndexAsync(int rowIndex)
    {
        var row = Root.Locator($"tr[data-index=\"{rowIndex}\"]");
        // Use the duration cell (plain text) as the click target to avoid hitting
        // anchors/svgs that suppress selection and trigger navigation.
        var safeCell = row.Locator("td[data-testid^='collection-cell-duration-']");
        await safeCell.ScrollIntoViewIfNeededAsync();
        var box = await safeCell.BoundingBoxAsync();
        if (box == null)
            throw new InvalidOperationException($"Row {rowIndex} not found");

        await Root.Page.Keyboard.DownAsync("Control");
        await Root.Page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Root.Page.Keyboard.UpAsync("Control");
    }

    /// <summary>
    /// Selects the rows containing each of the specified texts in the given column: clicks the first one, and
    /// ctrl-clicks the others. The clicks land on the <paramref name="clickColumnName"/> cell, which should hold
    /// plain text: anchors and icons suppress row selection.
    /// </summary>
    public async Task SelectRowsByCellTextAsync(string columnName, string clickColumnName, params string[] cellTexts)
    {
        foreach (var (cellText, index) in cellTexts.Select((text, index) => (text, index)))
        {
            // The filter is resolved inside each row, so the cell locator cannot be rooted at the collection
            var cell = Root.Page.Locator($"td[data-testid^='collection-cell-{columnName}-']")
                .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(cellText)}\s*$") });
            var row = Rows.Filter(new() { Has = cell }).First;
            var safeCell = row.Locator($"td[data-testid^='collection-cell-{clickColumnName}-']");

            await safeCell.ClickAsync(new()
            {
                Modifiers = index == 0 ? [] : [KeyboardModifier.Control],
            });
        }
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
        var cell = Root.Page.Locator($"td[data-testid^='collection-cell-{columnName}-']")
            .Filter(new LocatorFilterOptions { HasText = cellText });

        var row = Rows.Filter(new() { Has = cell }).First;
        await row.ClickAsync(new() { Button = MouseButton.Right });
    }

    /// <summary>
    /// Right-clicks the row containing the specified text in the given column, and clicks the action with
    /// the given name in the row's context menu.
    /// </summary>
    public async Task ClickRowActionAsync(string columnName, string cellText, string actionName)
    {
        await RightClickRowByCellTextAsync(columnName, cellText);
        await Root.Page.GetByRole(AriaRole.Menuitem, new() { Name = actionName, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// The cells of a column whose text is exactly the specified text.
    /// </summary>
    public ILocator GetCellsByExactText(string columnName, string cellText) =>
        Root.Locator($"td[data-testid^='collection-cell-{columnName}-']")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(cellText)}\s*$") });

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
    /// Clicks the Actions menu button in the floating bar and returns the open menu dropdown
    /// wrapped in a <see cref="SongsActionsMenuComponent"/>.
    /// </summary>
    public async Task<SongsActionsMenuComponent> OpenFloatingActionsMenuAsync()
    {
        var floatingBar = GetFloatingActionsBar();
        await floatingBar.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var actionsButton = floatingBar.GetByRole(AriaRole.Button, new() { Name = "Actions" });
        await actionsButton.ClickAsync();

        // The Mantine Menu dropdown is a sibling of the button, rendered in a portal
        var menuDropdown = Root.Page.Locator(".mantine-Menu-dropdown").Last;
        await menuDropdown.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        return new SongsActionsMenuComponent(menuDropdown);
    }
}
